import { computed, onUnmounted, ref } from 'vue'

export type Direction = 'up' | 'down' | 'left' | 'right'
export type Phase = 'building' | 'running' | 'won' | 'lost'
export type LostReason = 'wall' | 'offGrid' | 'incomplete'

export interface Cell {
  readonly row: number
  readonly col: number
}

interface Level {
  readonly cols: number
  readonly rows: number
  readonly start: Cell
  readonly goal: Cell
  readonly walls: readonly Cell[]
  readonly maxCommands: number
}

const STEP_MS = 550

interface LevelMap {
  readonly maxCommands: number
  readonly map: readonly string[]
}

const LEVEL_MAPS: readonly LevelMap[] = [
  {
    maxCommands: 11,
    map: ['.#..G', '.###.', '...#.', '##.#.', 'S....'],
  },
  {
    maxCommands: 15,
    map: ['.#..G', '.#.##', '.#...', '.###.', 'S....'],
  },
  {
    maxCommands: 15,
    map: ['.#....G', '.###.#.', '...#.#.', '##.#.#.', '...#.#.', '.###.#.', 'S....#.'],
  },
  {
    maxCommands: 19,
    map: ['.#....G', '.#.###.', '...#.#.', '####.#.', '.....#.', '.#.###.', 'S#.....'],
  },
  {
    maxCommands: 23,
    map: ['.#....G', '.###.#.', '.#...#.', '.#.####', '.#.....', '.#####.', 'S......'],
  },
]

function parseLevel({ map, maxCommands }: LevelMap): Level {
  const rows = map.length
  const cols = map[0]?.length ?? 0
  let start: Cell | null = null
  let goal: Cell | null = null
  const walls: Cell[] = []
  map.forEach((line, row) => {
    for (let col = 0; col < line.length; col += 1) {
      const ch = line[col]
      if (ch === 'S') start = { row, col }
      else if (ch === 'G') goal = { row, col }
      else if (ch === '#') walls.push({ row, col })
    }
  })
  if (!start || !goal) throw new Error('Level map must contain S and G')
  return { cols, rows, start, goal, walls, maxCommands }
}

const LEVELS = LEVEL_MAPS.map(parseLevel) as [Level, ...Level[]]

const DELTAS: Record<Direction, Cell> = {
  up: { row: -1, col: 0 },
  down: { row: 1, col: 0 },
  left: { row: 0, col: -1 },
  right: { row: 0, col: 1 },
}

function move(cell: Cell, dir: Direction): Cell {
  const delta = DELTAS[dir]
  return { row: cell.row + delta.row, col: cell.col + delta.col }
}

function sameCell(a: Cell, b: Cell): boolean {
  return a.row === b.row && a.col === b.col
}

function outOfBounds(cell: Cell, level: Level): boolean {
  return cell.row < 0 || cell.col < 0 || cell.row >= level.rows || cell.col >= level.cols
}

function hitsWall(cell: Cell, level: Level): boolean {
  return level.walls.some((wall) => sameCell(wall, cell))
}

export function useRobotGame() {
  const levelIndex = ref(0)
  const level = computed<Level>(() => LEVELS[levelIndex.value % LEVELS.length] ?? LEVELS[0])
  const commands = ref<Direction[]>([])
  const robot = ref<Cell>(LEVELS[0].start)
  const phase = ref<Phase>('building')
  const stepIndex = ref(-1)
  const lostReason = ref<LostReason | null>(null)
  let timer: ReturnType<typeof setInterval> | null = null

  const levelNumber = computed(() => levelIndex.value + 1)
  const totalLevels = LEVELS.length

  const boardCells = computed<Cell[]>(() => {
    const cells: Cell[] = []
    for (let row = 0; row < level.value.rows; row += 1) {
      for (let col = 0; col < level.value.cols; col += 1) {
        cells.push({ row, col })
      }
    }
    return cells
  })

  const canAdd = computed(
    () => phase.value === 'building' && commands.value.length < level.value.maxCommands,
  )
  const canRun = computed(() => phase.value === 'building' && commands.value.length > 0)
  const isRunning = computed(() => phase.value === 'running')

  function isWall(cell: Cell): boolean {
    return hitsWall(cell, level.value)
  }
  function isGoal(cell: Cell): boolean {
    return sameCell(cell, level.value.goal)
  }
  function isStart(cell: Cell): boolean {
    return sameCell(cell, level.value.start)
  }

  function stop(): void {
    if (timer !== null) {
      clearInterval(timer)
      timer = null
    }
  }

  function addCommand(dir: Direction): void {
    if (canAdd.value) commands.value.push(dir)
  }
  function undo(): void {
    if (phase.value === 'building') commands.value.pop()
  }
  function clearCommands(): void {
    if (phase.value === 'building') commands.value = []
  }

  function finish(reason: LostReason | null): void {
    stop()
    if (reason === null) {
      phase.value = 'won'
      stepIndex.value = -1
    } else {
      phase.value = 'lost'
      lostReason.value = reason
    }
  }

  function tick(): void {
    stepIndex.value += 1
    if (stepIndex.value >= commands.value.length) {
      finish('incomplete')
      return
    }
    const dir = commands.value[stepIndex.value]
    if (!dir) {
      finish('incomplete')
      return
    }
    const next = move(robot.value, dir)
    if (outOfBounds(next, level.value)) {
      finish('offGrid')
      return
    }
    if (hitsWall(next, level.value)) {
      finish('wall')
      return
    }
    robot.value = next
    if (sameCell(next, level.value.goal)) {
      finish(null)
    }
  }

  function run(): void {
    if (!canRun.value) return
    robot.value = level.value.start
    stepIndex.value = -1
    lostReason.value = null
    phase.value = 'running'
    stop()
    timer = setInterval(tick, STEP_MS)
  }

  function reset(): void {
    stop()
    robot.value = level.value.start
    stepIndex.value = -1
    lostReason.value = null
    phase.value = 'building'
  }

  function nextLevel(): void {
    levelIndex.value = (levelIndex.value + 1) % LEVELS.length
    commands.value = []
    reset()
  }

  onUnmounted(stop)

  return {
    level,
    levelNumber,
    totalLevels,
    boardCells,
    commands,
    robot,
    phase,
    stepIndex,
    lostReason,
    canAdd,
    canRun,
    isRunning,
    isWall,
    isGoal,
    isStart,
    addCommand,
    undo,
    clearCommands,
    run,
    reset,
    nextLevel,
  }
}
