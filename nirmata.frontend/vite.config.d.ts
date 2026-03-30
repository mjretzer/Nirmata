declare module 'node:fs' {
  export function existsSync(path: string): boolean
  export function readFileSync(path: string): any
}

declare module 'node:path' {
  export function resolve(...segments: string[]): string
}

declare const process: {
  cwd(): string
}
