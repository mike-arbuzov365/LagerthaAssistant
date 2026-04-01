export type ReadinessState = 'ok' | 'warn' | 'error'

export interface ReadinessSignal {
  id: string
  label: string
  state: ReadinessState
  details: string
}

export interface DashboardReadinessInput {
  locale: string
  defaultLocale: string
  oneDriveConfigured: boolean
  oneDriveAuthenticated: boolean
  aiHasStoredKey: boolean
  notionVocabularyEnabled: boolean
  notionFoodEnabled: boolean
}

export function buildReadinessSignals(input: DashboardReadinessInput): ReadinessSignal[] {
  const localeSignal: ReadinessSignal = {
    id: 'locale',
    label: 'Локалізація',
    state: input.locale === 'uk' ? 'ok' : 'warn',
    details: input.locale === 'uk'
      ? 'Інтерфейс працює на українській мові.'
      : `Поточна мова: ${input.locale}. Рекомендована за політикою: ${input.defaultLocale}.`,
  }

  const oneDriveSignal: ReadinessSignal = {
    id: 'onedrive',
    label: 'OneDrive',
    state: input.oneDriveConfigured && input.oneDriveAuthenticated
      ? 'ok'
      : input.oneDriveConfigured
        ? 'warn'
        : 'error',
    details: input.oneDriveConfigured && input.oneDriveAuthenticated
      ? 'OneDrive налаштовано і авторизовано.'
      : input.oneDriveConfigured
        ? 'OneDrive налаштовано, але не авторизовано.'
        : 'OneDrive не налаштовано.',
  }

  const aiSignal: ReadinessSignal = {
    id: 'ai',
    label: 'AI',
    state: input.aiHasStoredKey ? 'ok' : 'warn',
    details: input.aiHasStoredKey
      ? 'Ключ AI провайдера збережений.'
      : 'Ключ AI провайдера не збережений.',
  }

  const integrationSignal: ReadinessSignal = {
    id: 'integrations',
    label: 'Інтеграції',
    state: input.notionVocabularyEnabled || input.notionFoodEnabled ? 'ok' : 'warn',
    details: input.notionVocabularyEnabled || input.notionFoodEnabled
      ? 'Принаймні одна інтеграція Notion увімкнена.'
      : 'Інтеграції Notion вимкнені.',
  }

  return [localeSignal, oneDriveSignal, aiSignal, integrationSignal]
}

export function readinessClassName(state: ReadinessState): string {
  switch (state) {
    case 'ok':
      return 'status-ok'
    case 'warn':
      return 'status-warn'
    default:
      return 'status-error'
  }
}

