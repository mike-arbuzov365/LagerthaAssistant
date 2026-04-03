export type AppThemeMode = 'system' | 'light' | 'dark'
export type HostTheme = 'light' | 'dark'

export function normalizeThemeMode(input: string | null | undefined): AppThemeMode {
  const normalized = input?.trim().toLowerCase()

  if (normalized === 'light' || normalized === 'dark') {
    return normalized
  }

  return 'system'
}

export function resolveAppliedTheme(
  themeMode: AppThemeMode,
  hostTheme: HostTheme,
): HostTheme {
  return themeMode === 'system' ? hostTheme : themeMode
}
