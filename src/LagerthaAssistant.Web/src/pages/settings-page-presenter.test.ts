import { describe, expect, it } from 'vitest'
import {
  buildLanguageChoices,
  buildSaveModeChoices,
  formatModelLabel,
  formatProviderLabel,
  resolveNotionVisual,
  resolveOneDriveVisual,
} from './settings-page-presenter'

describe('settings-page-presenter', () => {
  it('builds Ukrainian language choices with Ukrainian default copy', () => {
    const options = buildLanguageChoices('uk')

    expect(options).toHaveLength(2)
    expect(options[0]).toMatchObject({
      value: 'uk',
      title: 'Українська',
    })
  })

  it('builds descriptive save mode choices', () => {
    const options = buildSaveModeChoices('en', ['ask', 'auto', 'off'])

    expect(options.map((option) => option.title)).toEqual([
      'Ask before save',
      'Automatic',
      'Disabled',
    ])
    expect(options[0]?.description).toContain('confirmation')
  })

  it('formats provider and model labels for cleaner UI', () => {
    expect(formatProviderLabel('openai')).toBe('OpenAI')
    expect(formatProviderLabel('claude')).toBe('Claude')
    expect(formatModelLabel('gpt-4.1-mini')).toBe('GPT-4.1 mini')
    expect(formatModelLabel('claude-3-5-sonnet-latest')).toBe('Claude 3 5 sonnet latest')
  })

  it('returns connected OneDrive visual when authorized', () => {
    const visual = resolveOneDriveVisual(
      { isConfigured: true, isAuthenticated: true },
      'uk',
    )

    expect(visual.tone).toBe('ok')
    expect(visual.label).toBe('Підключено')
  })

  it('returns prepared Notion visual when config exists but flow is disabled', () => {
    const visual = resolveNotionVisual(
      {
        vocabularyEnabled: false,
        vocabularyConfigured: true,
        foodEnabled: false,
        foodConfigured: false,
      },
      'en',
    )

    expect(visual.tone).toBe('warn')
    expect(visual.label).toBe('Prepared')
  })
})
