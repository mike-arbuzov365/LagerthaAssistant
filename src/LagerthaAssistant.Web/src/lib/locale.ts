export type AppLocale = 'uk' | 'en'

export function normalizeLocale(input: string | null | undefined): AppLocale | null {
  if (!input) {
    return null
  }

  const value = input.trim().toLowerCase()
  if (value.startsWith('uk') || value.startsWith('ua')) {
    return 'uk'
  }

  if (value.startsWith('en')) {
    return 'en'
  }

  return null
}

export function resolvePreferredLocale(
  persistedLocale: string | null | undefined,
  hostLanguageCode: string | null | undefined,
): AppLocale {
  return normalizeLocale(persistedLocale)
    ?? normalizeLocale(hostLanguageCode)
    ?? 'uk'
}
