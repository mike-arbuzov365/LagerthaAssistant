import { describe, expect, it } from 'vitest'
import { normalizeLocale, resolvePreferredLocale } from './locale'

describe('normalizeLocale', () => {
  it('returns uk for ukrainian codes', () => {
    expect(normalizeLocale('uk')).toBe('uk')
    expect(normalizeLocale('uk-UA')).toBe('uk')
    expect(normalizeLocale('ua')).toBe('uk')
  })

  it('returns en for english codes', () => {
    expect(normalizeLocale('en')).toBe('en')
    expect(normalizeLocale('en-US')).toBe('en')
  })

  it('returns null for unsupported locale', () => {
    expect(normalizeLocale('de')).toBeNull()
  })
})

describe('resolvePreferredLocale', () => {
  it('prefers persisted locale', () => {
    expect(resolvePreferredLocale('en', 'uk')).toBe('en')
  })

  it('falls back to host locale', () => {
    expect(resolvePreferredLocale(null, 'en-US')).toBe('en')
  })

  it('defaults to ukrainian', () => {
    expect(resolvePreferredLocale(null, null)).toBe('uk')
  })
})
