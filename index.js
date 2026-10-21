import { spawn } from 'node:child_process'
import {
  appendFileSync,
  chmodSync,
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs'
import { createServer } from 'node:net'
import { homedir } from 'node:os'
import { basename, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const PROTOCOL_VERSION = 1
const PLUGIN_ROOT = fileURLToPath(new URL('.', import.meta.url))
const SUPPORT_DIR = join(homedir(), 'Library', 'Application Support', 'DSHNotchPet')
const LOG_DIR = join(homedir(), 'Library', 'Logs')
const HOST_LOG = join(LOG_DIR, 'DSHNotchPet-host.log')
const NATIVE_BINARY = join(
  PLUGIN_ROOT,
  'native',
  'DSHNotchPet.app',
  'Contents',
  'MacOS',
  'DSHNotchPet',
)
const CURRENT_FILE = join(SUPPORT_DIR, 'current.json')
const MAX_SESSIONS = 6
const MAX_TITLE_CHARS = 120
const MAX_TODOS = 12

export function apply(ctx) {
  if (process.platform !== 'darwin') return
  const bridge = new NotchPetBridge(ctx)
  ctx.effect(() => {
    bridge.start()
    return () => bridge.stop()
  })
}

class NotchPetBridge {
  #ctx
  #states = new Map()
  #clients = new Set()
  #disposers = []
  #server
  #socketPath = ''
  #child
  #publishTimer
  #heartbeat
  #stopping = false

  constructor(ctx) {
    this.#ctx = ctx
  }

  start() {
    try {
      mkdirSync(SUPPORT_DIR, { recursive: true, mode: 0o700 })
      mkdirSync(LOG_DIR, { recursive: true })
      this.#socketPath = join(SUPPORT_DIR, `pet-${process.pid}.sock`)
      rmSync(this.#socketPath, { force: true })
      this.#start().catch((error) => this.#log(`start failed: ${error?.message ?? error}`))
    } catch (error) {
      this.#log(`init failed: ${error?.message ?? error}`)
    }
  }

  async #start() {
    this.#registerEvents()
    this.#hydrateLiveAgents()
    await this.#listen()
    if (this.#stopping) return
    this.#spawnNative()
    this.#publish({ immediate: true })
    this.#heartbeat = setInterval(() => this.#publish({ immediate: true }), 5000)
    this.#heartbeat.unref?.()
    this.#log(`started pid=${process.pid} socket=${this.#socketPath}`)
  }

  stop() {
    if (this.#stopping) return
    this.#stopping = true
    clearTimeout(this.#publishTimer)
    clearInterval(this.#heartbeat)
    for (const dispose of this.#disposers.splice(0)) {
      try {
        dispose()
      } catch {}
    }
    for (const client of this.#clients) {
      try {
        client.destroy()
      } catch {}
    }
    this.#clients.clear()
    this.#server?.close()
    if (this.#child && !this.#child.killed) {
      try {
        this.#child.kill('SIGTERM')
      } catch {}
    }
    rmSync(this.#socketPath, { force: true })
    try {
      const current = JSON.parse(readText(CURRENT_FILE) ?? 'null')
      if (current?.pid === process.pid) rmSync(CURRENT_FILE, { force: true })
    } catch {}
    this.#log('stopped')
  }

  #registerEvents() {
    const on = (name, listener) => {
      this.#disposers.push(this.#ctx.on(name, listener))
    }

    on('agent/created', ({ agent }) => {
      this.#ensureState(agent)
      this.#refreshGoal(agent)
      this.#schedulePublish()
    })

    on('agent/status', ({ agent, status }) => {
      const state = this.#ensureState(agent)
      state.running = status === 'running'
      state.updatedAt = Date.now()
      if (state.running) {
        state.turnStartedAt ||= Date.now()
        state.lastError = null
      } else {
        state.activeTools.clear()
        state.completedAt = Date.now()
        if (!state.lastError) state.phase = 'done'
      }
      this.#schedulePublish()
    })

    on('agent/disposed', ({ agent }) => {
      this.#states.delete(agent.id)
      this.#schedulePublish()
    })

    on('agent/error', ({ agent, error }) => {
      const state = this.#ensureState(agent)
      state.lastError = errorMessage(error)
      state.phase = 'error'
      state.updatedAt = Date.now()
      this.#schedulePublish()
    })

    on('goal/changed', ({ agent, change }) => {
      const state = this.#ensureState(agent)
      state.goal = change?.goal ?? null
      state.updatedAt = Date.now()
      this.#schedulePublish()
    })

    on('session/event', (session, event) => {
      const state = this.#states.get(session.id)
      if (!state) return
      this.#applyEvent(state, event)
      this.#schedulePublish()
    })
  }

  #hydrateLiveAgents() {
    const agents = this.#service('agents')?.list?.() ?? []
    for (const agent of agents) {
      const state = this.#ensureState(agent)
      const session = this.#sessionFor(agent.id)
      const events = readSessionEvents(session)
      for (const event of events) this.#applyEvent(state, event)
      state.running = sessionIsOpen(events)
      if (!state.running) {
        state.phase = state.lastError ? 'error' : state.completedAt ? 'done' : 'idle'
      }
      state.updatedAt = events.at(-1)?.time ?? state.createdAt
      state.model = session?.requestContext?.()?.model ?? state.model
      this.#refreshGoal(agent)
    }
  }

  #ensureState(agent) {
    const id = String(agent.id)
    const known = this.#states.get(id)
    if (known) return known
    const session = this.#sessionFor(id)
    const header = session?.header
    const state = {
      id,
      title: '',
      cwd: header?.cwd ?? '',
      root: header?.origin !== 'subagent',
      agentPreset: header?.agentPreset ?? '',
      model: session?.requestContext?.()?.model ?? '',
      running: false,
      phase: 'idle',
      turn: 0,
      step: 0,
      activeTools: new Map(),
      lastTool: '',
      lastError: null,
      goal: null,
      todos: [],
      turnStartedAt: 0,
      completedAt: 0,
      updatedAt: header?.createdAt ?? Date.now(),
      createdAt: header?.createdAt ?? Date.now(),
    }
    this.#states.set(id, state)
    return state
  }

  #applyEvent(state, event) {
    if (!event || typeof event !== 'object') return
    const time = Number(event.time) || Date.now()
    switch (event.type) {
      case 'user/message': {
        if (!state.title) {
          state.title = clip(textOf(event.data?.content), MAX_TITLE_CHARS)
        }
        state.updatedAt = time
        break
      }
      case 'turn/start': {
        state.turn = Number(event.data?.turn) || state.turn
        state.step = 0
        state.running = true
        state.phase = 'thinking'
        state.turnStartedAt = time
        state.lastError = null
        state.activeTools.clear()
        state.updatedAt = time
        break
      }
      case 'step/start': {
        state.step = Number(event.data?.step) || state.step
        state.phase = 'thinking'
        state.updatedAt = time
        break
      }
      case 'assistant/message': {
        state.phase = 'thinking'
        state.updatedAt = time
        break
      }
      case 'tool/call': {
        const callId = String(event.data?.callId ?? '')
        const name = String(event.data?.name ?? 'tool')
        if (callId) state.activeTools.set(callId, { name, startedAt: time })
        state.lastTool = name
        state.phase = name === 'ask_user_question' ? 'waiting' : 'tool'
        state.updatedAt = time
        if (name === 'todo_write') {
          const todos = parseTodos(event.data?.arguments)
          if (todos) state.todos = todos
        }
        break
      }
      case 'tool/result': {
        const callId = String(event.data?.message?.toolCallId ?? '')
        if (callId) state.activeTools.delete(callId)
        if (event.data?.error) {
          state.lastError = clip(
            String(event.data.error.reason || event.data.error.code || 'Tool failed'),
            200,
          )
        }
        state.phase = state.activeTools.size > 0 ? 'tool' : 'thinking'
        state.updatedAt = time
        break
      }
      case 'turn/end': {
        state.activeTools.clear()
        const reason = event.data?.reason
        if (reason?.kind === 'error') {
          state.lastError = clip(errorMessage(reason.error), 200)
          state.phase = 'error'
        } else {
          state.lastError = null
          state.phase = 'done'
        }
        state.completedAt = time
        state.updatedAt = time
        break
      }
      default:
        break
    }
  }

  #sessionFor(id) {
    return this.#service('sessions')?.get?.(id)
  }

  #service(name) {
    try {
      return this.#ctx.get(name)
    } catch {
      return undefined
    }
  }

  #refreshGoal(agent) {
    const state = this.#states.get(String(agent.id))
    if (!state) return
    const goals = this.#service('goals')
    if (!goals) return
    try {
      state.goal = goals.get(agent) ?? null
    } catch {
      state.goal = null
    }
  }

  #schedulePublish() {
    if (this.#stopping) return
    clearTimeout(this.#publishTimer)
    this.#publishTimer = setTimeout(() => this.#publish(), 120)
    this.#publishTimer.unref?.()
  }

  #publish({ immediate = false } = {}) {
    if (!this.#server || this.#clients.size === 0) return
    const payload = JSON.stringify(this.#snapshot(Date.now()))
    for (const client of this.#clients) {
      try {
        client.write(`${payload}\n`)
      } catch {
        this.#clients.delete(client)
      }
    }
    if (immediate) this.#writeCurrentFile()
  }

  #snapshot(now) {
    const states = [...this.#states.values()]
    const running = states.filter((state) => state.running)
    const roots = states.filter((state) => state.root)
    const pool = running.length > 0 ? running : roots.length > 0 ? roots : states
    const focus = [...pool].sort((a, b) => {
      if (a.root !== b.root) return a.root ? -1 : 1
      return b.updatedAt - a.updatedAt
    })[0]

    const sessions = [...states]
      .sort((a, b) => {
        if (a.running !== b.running) return a.running ? -1 : 1
        return b.updatedAt - a.updatedAt
      })
      .slice(0, MAX_SESSIONS)
      .map((state) => ({
        id: state.id,
        title: sessionTitle(state),
        running: state.running,
        phase: effectivePhase(state, now),
        tool: activeToolName(state),
        updatedAt: state.updatedAt,
        root: state.root,
      }))

    return {
      v: PROTOCOL_VERSION,
      now,
      origin: this.#origin(),
      overall: overallStatus(focus, running.length, now),
      activeCount: running.length,
      totalCount: states.length,
      focus: focus ? focusView(focus, now) : null,
      sessions,
    }
  }

  #origin() {
    try {
      return this.#service('webServer')?.getOrigin?.() ?? ''
    } catch {
      return ''
    }
  }

  async #listen() {
    await new Promise((resolve) => {
      const server = createServer((socket) => {
        socket.setNoDelay(true)
        this.#clients.add(socket)
        socket.on('close', () => this.#clients.delete(socket))
        socket.on('error', () => this.#clients.delete(socket))
        this.#publish({ immediate: true })
      })
      server.on('error', (error) => {
        this.#log(`socket error: ${error?.message ?? error}`)
        resolve()
      })
      server.listen(this.#socketPath, () => {
        try {
          chmodSync(this.#socketPath, 0o600)
        } catch {}
        this.#server = server
        this.#writeCurrentFile()
        resolve()
      })
    })
  }

  #writeCurrentFile() {
    try {
      writeFileSync(
        CURRENT_FILE,
        JSON.stringify({
          pid: process.pid,
          socket: this.#socketPath,
          origin: this.#origin(),
          updatedAt: Date.now(),
        }),
        { mode: 0o600 },
      )
      chmodSync(CURRENT_FILE, 0o600)
    } catch {}
  }

  #spawnNative() {
    if (process.env.DSH_NOTCH_PET_DISABLE_OVERLAY === '1') {
      this.#log('native overlay disabled by environment')
      return
    }
    if (!existsSync(NATIVE_BINARY)) {
      this.#log(`native binary missing: ${NATIVE_BINARY}`)
      return
    }
    try {
      this.#child = spawn(NATIVE_BINARY, ['--socket', this.#socketPath], {
        cwd: PLUGIN_ROOT,
        detached: false,
        stdio: 'ignore',
      })
      this.#child.unref()
      this.#log(`spawned native pid=${this.#child.pid}`)
    } catch (error) {
      this.#log(`spawn failed: ${error?.message ?? error}`)
    }
  }

  #log(line) {
    try {
      appendFileSync(HOST_LOG, `${new Date().toISOString()} ${line}\n`)
    } catch {}
  }
}

function readSessionEvents(session) {
  try {
    return session?.snapshotEvents?.() ?? []
  } catch {
    return []
  }
}

function sessionIsOpen(events) {
  for (let index = events.length - 1; index >= 0; index -= 1) {
    const type = events[index]?.type
    if (type === 'turn/start') return true
    if (type === 'turn/end') return false
  }
  return false
}

function activeToolName(state) {
  const tools = [...state.activeTools.values()].sort((a, b) => b.startedAt - a.startedAt)
  return tools[0]?.name ?? ''
}

function effectivePhase(state, now) {
  if (state.running) {
    if (activeToolName(state) === 'ask_user_question') return 'waiting'
    if (state.activeTools.size > 0) return 'tool'
    return 'thinking'
  }
  if (state.lastError) return 'error'
  if (state.completedAt && now - state.completedAt < 12000) return 'done'
  return 'idle'
}

function overallStatus(focus, activeCount, now) {
  if (focus && effectivePhase(focus, now) === 'waiting') return 'waiting'
  if (activeCount > 0) return 'running'
  if (focus?.lastError) return 'error'
  if (focus?.completedAt && now - focus.completedAt < 12000) return 'done'
  return 'idle'
}

function focusView(state, now) {
  const phase = effectivePhase(state, now)
  const tool = activeToolName(state)
  const todos = state.todos ?? []
  const completedTodos = todos.filter((todo) => todo.status === 'completed').length
  const goal = state.goal
  let progress = null
  if (todos.length > 0) {
    progress = {
      kind: 'todo',
      completed: completedTodos,
      total: todos.length,
      label: currentTodo(todos),
    }
  } else if (goal && Number(goal.maxGoalRounds) > 0) {
    progress = {
      kind: 'goal',
      completed: Number(goal.roundsStarted) || 0,
      total: Number(goal.maxGoalRounds) || 0,
      label: clip(goal.objective ?? '', 160),
    }
  }
  return {
    id: state.id,
    title: sessionTitle(state),
    cwd: state.cwd,
    root: state.root,
    agentPreset: state.agentPreset,
    model: state.model,
    running: state.running,
    phase,
    tool,
    toolCount: state.activeTools.size,
    turn: state.turn,
    step: state.step,
    startedAt: state.turnStartedAt || 0,
    completedAt: state.completedAt || 0,
    updatedAt: state.updatedAt,
    lastTool: state.lastTool,
    lastError: state.lastError ? clip(String(state.lastError), 200) : '',
    progress,
    goal: goal
      ? {
          phase: goal.phase ?? 'active',
          objective: clip(goal.objective ?? '', 240),
          roundsStarted: Number(goal.roundsStarted) || 0,
          maxGoalRounds: Number(goal.maxGoalRounds) || 0,
        }
      : null,
    todos: todos.slice(0, MAX_TODOS),
  }
}

function sessionTitle(state) {
  const title = String(state.title ?? '').trim()
  if (title) return title
  const folder = basename(String(state.cwd ?? ''))
  return folder || 'DSH session'
}

function currentTodo(todos) {
  const active = todos.find((todo) => todo.status === 'in_progress')
  return clip(active?.content ?? todos.find((todo) => todo.status === 'pending')?.content ?? '', 120)
}

function parseTodos(raw) {
  try {
    const parsed = JSON.parse(String(raw ?? ''))
    if (!Array.isArray(parsed?.todos)) return undefined
    return parsed.todos
      .filter((todo) => todo && typeof todo === 'object')
      .map((todo) => ({
        content: clip(String(todo.content ?? ''), 160),
        status: ['pending', 'in_progress', 'completed'].includes(todo.status)
          ? todo.status
          : 'pending',
      }))
      .slice(0, MAX_TODOS)
  } catch {
    return undefined
  }
}

function textOf(blocks) {
  if (!Array.isArray(blocks)) return ''
  return blocks
    .filter((block) => block?.type === 'text' && typeof block.text === 'string')
    .map((block) => block.text)
    .join(' ')
    .replace(/\s+/g, ' ')
    .trim()
}

function errorMessage(error) {
  if (!error) return 'Unknown error'
  if (typeof error === 'string') return error
  if (typeof error.message === 'string') return error.message
  return String(error)
}

function clip(text, length) {
  const value = String(text ?? '').trim()
  return value.length > length ? `${value.slice(0, length - 1)}…` : value
}

function readText(path) {
  try {
    return readFileSync(path, 'utf8')
  } catch {
    return undefined
  }
}
