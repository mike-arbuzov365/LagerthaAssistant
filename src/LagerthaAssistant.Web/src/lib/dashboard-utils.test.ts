import { describe, expect, it } from 'vitest'
import { buildReadinessSignals, readinessClassName } from './dashboard-utils'

describe('buildReadinessSignals', () => {
  it('returns all green statuses for ready setup', () => {
    const signals = buildReadinessSignals({
      locale: 'uk',
      defaultLocale: 'uk',
      oneDriveConfigured: true,
      oneDriveAuthenticated: true,
      aiHasStoredKey: true,
      notionVocabularyEnabled: true,
      notionFoodEnabled: false,
    })

    expect(signals.map((signal) => signal.state)).toEqual(['ok', 'ok', 'ok', 'ok'])
  })

  it('returns warning and error statuses for not-ready setup', () => {
    const signals = buildReadinessSignals({
      locale: 'en',
      defaultLocale: 'uk',
      oneDriveConfigured: false,
      oneDriveAuthenticated: false,
      aiHasStoredKey: false,
      notionVocabularyEnabled: false,
      notionFoodEnabled: false,
    })

    expect(signals.map((signal) => signal.state)).toEqual(['warn', 'error', 'warn', 'warn'])
  })
})

describe('readinessClassName', () => {
  it('maps warn state to warn class', () => {
    expect(readinessClassName('warn')).toBe('status-warn')
  })
})

