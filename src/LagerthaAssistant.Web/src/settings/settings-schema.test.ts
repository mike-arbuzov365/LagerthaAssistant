import { describe, expect, it } from 'vitest'
import { buildSettingsSchema } from './settings-schema'

describe('buildSettingsSchema', () => {
  it('returns only core sections when only core module is requested', () => {
    const schema = buildSettingsSchema({ modules: ['core'] })

    expect(schema.map((section) => section.id)).toEqual(['state', 'locale', 'session'])
  })

  it('adds lagertha sections when lagertha module is requested', () => {
    const schema = buildSettingsSchema({ modules: ['core', 'lagertha'] })

    expect(schema.map((section) => section.id)).toEqual([
      'state',
      'locale',
      'session',
      'ai',
      'policy',
      'integrations',
      'onedriveAuth',
      'onedriveOps',
    ])
  })

  it('deduplicates modules and keeps predictable order', () => {
    const schema = buildSettingsSchema({ modules: ['core', 'core', 'lagertha'] })

    expect(schema.map((section) => section.id)).toEqual([
      'state',
      'locale',
      'session',
      'ai',
      'policy',
      'integrations',
      'onedriveAuth',
      'onedriveOps',
    ])
  })
})
