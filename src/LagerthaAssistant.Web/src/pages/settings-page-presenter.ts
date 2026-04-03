import type { AppLocale } from '../lib/locale'

export interface SettingChoiceOption {
  value: string
  title: string
  description?: string
  icon?: string
}

export type IntegrationTone = 'ok' | 'warn' | 'error'

export interface IntegrationVisual {
  tone: IntegrationTone
  label: string
  description: string
}

interface NotionVisualInput {
  vocabularyEnabled: boolean
  vocabularyConfigured: boolean
  foodEnabled: boolean
  foodConfigured: boolean
}

interface OneDriveVisualInput {
  isConfigured: boolean
  isAuthenticated: boolean
}

const saveModeLabels = {
  uk: {
    auto: 'Автоматично',
    ask: 'Запитувати перед збереженням',
    off: 'Вимкнено',
  },
  en: {
    auto: 'Automatic',
    ask: 'Ask before save',
    off: 'Disabled',
  },
} as const

const saveModeDescriptions = {
  uk: {
    auto: 'Бот сам зберігає зміни без додаткового запиту.',
    ask: 'Показує підтвердження перед записом у сховище.',
    off: 'Нове не зберігається, лише тимчасова сесія.',
  },
  en: {
    auto: 'The bot saves changes immediately without asking again.',
    ask: 'Shows a confirmation before writing to storage.',
    off: 'No new data is persisted, session only.',
  },
} as const

const storageModeLabels = {
  uk: {
    graph: 'Graph (OneDrive)',
    local: 'Локальне сховище',
  },
  en: {
    graph: 'Graph (OneDrive)',
    local: 'Local storage',
  },
} as const

const storageModeDescriptions = {
  uk: {
    graph: 'Основний cloud-режим для синхронізації між сесіями.',
    local: 'Локальна робота без зовнішньої синхронізації.',
  },
  en: {
    graph: 'Primary cloud mode for cross-session sync.',
    local: 'Local-only mode without external sync.',
  },
} as const

const aiKeySourceLabels = {
  uk: {
    stored: 'Збережений ключ',
    environment: 'Ключ із середовища',
    missing: 'Ключ відсутній',
  },
  en: {
    stored: 'Stored key',
    environment: 'Environment key',
    missing: 'Key missing',
  },
} as const

export function mapSaveMode(mode: string, locale: AppLocale): string {
  return saveModeLabels[locale][mode as keyof typeof saveModeLabels.uk] ?? mode
}

export function mapStorageMode(mode: string, locale: AppLocale): string {
  return storageModeLabels[locale][mode as keyof typeof storageModeLabels.uk] ?? mode
}

export function mapAiKeySource(source: string, locale: AppLocale): string {
  return aiKeySourceLabels[locale][source as keyof typeof aiKeySourceLabels.uk] ?? source
}

export function buildLanguageChoices(locale: AppLocale): SettingChoiceOption[] {
  if (locale === 'uk') {
    return [
      {
        value: 'uk',
        title: 'Українська',
        icon: 'flag-uk',
      },
      {
        value: 'en',
        title: 'English',
        icon: 'flag-gb',
      },
    ]
  }

  return [
    {
      value: 'uk',
      title: 'Ukrainian',
      icon: 'flag-uk',
    },
    {
      value: 'en',
      title: 'English',
      icon: 'flag-gb',
    },
  ]
}

export function buildThemeChoices(locale: AppLocale): SettingChoiceOption[] {
  if (locale === 'uk') {
    return [
      {
        value: 'system',
        title: 'Системна',
        icon: 'theme-system',
      },
      {
        value: 'light',
        title: 'Світла',
        icon: 'theme-light',
      },
      {
        value: 'dark',
        title: 'Темна',
        icon: 'theme-dark',
      },
    ]
  }

  return [
    {
      value: 'system',
      title: 'System',
      icon: 'theme-system',
    },
    {
      value: 'light',
      title: 'Light',
      icon: 'theme-light',
    },
    {
      value: 'dark',
      title: 'Dark',
      icon: 'theme-dark',
    },
  ]
}

export function buildSaveModeChoices(
  locale: AppLocale,
  supportedModes: readonly string[],
): SettingChoiceOption[] {
  return supportedModes.map((mode) => ({
    value: mode,
    title: mapSaveMode(mode, locale),
    description: saveModeDescriptions[locale][mode as keyof typeof saveModeDescriptions.uk] ?? mode,
  }))
}

export function buildStorageModeChoices(
  locale: AppLocale,
  supportedModes: readonly string[],
): SettingChoiceOption[] {
  return supportedModes.map((mode) => ({
    value: mode,
    title: mapStorageMode(mode, locale),
    description: storageModeDescriptions[locale][mode as keyof typeof storageModeDescriptions.uk] ?? mode,
  }))
}

export function formatProviderLabel(provider: string): string {
  const normalized = provider.trim().toLowerCase()

  return normalized === 'openai'
    ? 'OpenAI'
    : normalized === 'claude'
      ? 'Claude'
      : provider
}

export function formatModelLabel(model: string): string {
  const normalized = model.trim()
  if (!normalized) {
    return model
  }

  if (/^gpt-/i.test(normalized)) {
    return `GPT-${normalized.slice(4).replace(/-/g, ' ').replace(/\s+/g, ' ').trim()}`
  }

  if (/^claude-/i.test(normalized)) {
    return `Claude ${normalized.slice(7).replace(/-/g, ' ').replace(/\s+/g, ' ').trim()}`
  }

  return normalized.replace(/-/g, ' ').replace(/\s+/g, ' ').trim()
}

export function resolveOneDriveVisual(
  input: OneDriveVisualInput,
  locale: AppLocale,
): IntegrationVisual {
  if (!input.isConfigured) {
    return locale === 'uk'
      ? {
          tone: 'warn',
          label: 'Не налаштовано',
          description: 'Сервіс ще не має конфігурації. Синхронізація поки недоступна.',
        }
      : {
          tone: 'warn',
          label: 'Not configured',
          description: 'The service is missing configuration, so sync is unavailable.',
        }
  }

  if (input.isAuthenticated) {
    return locale === 'uk'
      ? {
          tone: 'ok',
          label: 'Підключено',
          description: 'Cloud sync активний. Можна оновлювати індекс і запускати синхронізацію.',
        }
      : {
          tone: 'ok',
          label: 'Connected',
          description: 'Cloud sync is active. Index rebuild and sync actions are available.',
        }
  }

  return locale === 'uk'
    ? {
        tone: 'error',
        label: 'Потрібен вхід',
        description: 'Підключення налаштоване, але потрібно завершити авторизацію в OneDrive.',
      }
    : {
        tone: 'error',
        label: 'Sign-in required',
        description: 'The integration is configured, but OneDrive authorization must be completed.',
      }
}

export function resolveNotionVisual(
  input: NotionVisualInput,
  locale: AppLocale,
): IntegrationVisual {
  const hasConfiguredFlow = input.vocabularyConfigured || input.foodConfigured
  const hasEnabledFlow = input.vocabularyEnabled || input.foodEnabled

  if (hasEnabledFlow) {
    return locale === 'uk'
      ? {
          tone: 'ok',
          label: 'Активно',
          description: 'Щонайменше один Notion-потік увімкнений і готовий для роботи.',
        }
      : {
          tone: 'ok',
          label: 'Active',
          description: 'At least one Notion flow is enabled and ready to work.',
        }
  }

  if (hasConfiguredFlow) {
    return locale === 'uk'
      ? {
          tone: 'warn',
          label: 'Підготовлено',
          description: 'Конфігурація є, але потоки ще не увімкнені для цього сценарію.',
        }
      : {
          tone: 'warn',
          label: 'Prepared',
          description: 'Configuration exists, but the flows are not enabled for this scenario yet.',
        }
  }

  return locale === 'uk'
    ? {
        tone: 'warn',
        label: 'Очікує підключення',
        description: 'Інтеграцію ще не налаштовано. Можна підготувати її для наступних ботів.',
      }
    : {
        tone: 'warn',
        label: 'Waiting for setup',
        description: 'The integration is not configured yet, but can be prepared for future bots.',
      }
}
