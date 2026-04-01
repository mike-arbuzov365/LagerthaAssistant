import { describe, expect, it } from 'vitest'
import { formatAiKeySource, getScopedUserId } from './settings-utils'

describe('formatAiKeySource', () => {
  it('formats stored source in ukrainian', () => {
    expect(formatAiKeySource('stored')).toBe('Збережений ключ')
  })

  it('formats environment source in ukrainian', () => {
    expect(formatAiKeySource('environment')).toBe('Ключ із середовища')
  })

  it('falls back to missing for unknown source', () => {
    expect(formatAiKeySource('unknown')).toBe('Ключ відсутній')
  })
})

describe('getScopedUserId', () => {
  it('returns null for anonymous', () => {
    expect(getScopedUserId('anonymous')).toBeNull()
  })

  it('returns null for empty value', () => {
    expect(getScopedUserId(undefined)).toBeNull()
  })

  it('returns user id for real user', () => {
    expect(getScopedUserId('12345')).toBe('12345')
  })
})
