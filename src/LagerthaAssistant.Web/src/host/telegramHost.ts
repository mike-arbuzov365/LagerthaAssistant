import type { HostTheme } from '../lib/theme'
import type { HostContext, HostPlatform } from './types'

declare global {
  interface Window {
    Telegram?: {
      WebView?: {
        postEvent?: (eventType: string, callback?: boolean, eventData?: Record<string, unknown>) => void
      }
      WebApp?: {
        initData?: string
        initDataUnsafe?: {
          user?: {
            id?: number
            language_code?: string
          }
        }
        colorScheme?: 'light' | 'dark'
        platform?: string
        contentSafeAreaInsets?: { top?: number }
        isExpanded?: boolean
        isFullscreen?: boolean
        ready(): void
        expand(): void
        requestFullscreen?: () => void
        onEvent?: (eventType: string, handler: (payload?: unknown) => void) => void
        offEvent?: (eventType: string, handler: (payload?: unknown) => void) => void
      }
    }
  }
}

function resolveTheme(value: string | undefined): HostTheme {
  return value === 'dark' ? 'dark' : 'light'
}

function resolvePlatform(value: string | undefined): HostPlatform {
  const normalized = value?.trim().toLowerCase()

  return normalized === 'android' || normalized === 'ios' || normalized === 'tdesktop'
    ? (normalized === 'tdesktop' ? 'desktop' : normalized)
    : 'unknown'
}

function parseInitDataUser(
  initData: string | undefined,
): {
  id: string | null
  languageCode: string | null
} {
  if (!initData) {
    return { id: null, languageCode: null }
  }

  try {
    const params = new URLSearchParams(initData)
    const userPayload = params.get('user')
    if (!userPayload) {
      return { id: null, languageCode: null }
    }

    const parsed = JSON.parse(userPayload) as {
      id?: number | string
      language_code?: string
    }

    const id = typeof parsed.id === 'number'
      ? String(parsed.id)
      : (typeof parsed.id === 'string' && parsed.id.trim().length > 0 ? parsed.id.trim() : null)

    const languageCode = typeof parsed.language_code === 'string' && parsed.language_code.trim().length > 0
      ? parsed.language_code.trim()
      : null

    return { id, languageCode }
  } catch {
    return { id: null, languageCode: null }
  }
}

function requestPreferredViewport(
  webApp: NonNullable<NonNullable<typeof window.Telegram>['WebApp']>,
  platform: HostPlatform,
) {
  webApp.expand()

  if (platform === 'android' || platform === 'ios') {
    try {
      webApp.requestFullscreen?.()
    } catch {
      // Best-effort only.
    }
  }
}

export function createTelegramHost(): HostContext | null {
  const webApp = window.Telegram?.WebApp
  if (!webApp) {
    return null
  }

  const user = webApp.initDataUnsafe?.user
  const parsedInitDataUser = parseInitDataUser(webApp.initData)
  const userId = typeof user?.id === 'number' ? String(user.id) : parsedInitDataUser.id
  const userLanguageCode = user?.language_code ?? parsedInitDataUser.languageCode
  const initData = webApp.initData?.trim() ?? ''

  if (!userId || initData.length === 0) {
    return null
  }

  const platform = resolvePlatform(webApp.platform)

  return {
    isTelegram: true,
    theme: resolveTheme(webApp.colorScheme),
    platform,
    safeAreaTop: Math.max(0, webApp.contentSafeAreaInsets?.top ?? 0),
    initData,
    userLanguageCode,
    userId,
    conversationId: userId,
    ready() {
      webApp.ready()
      requestPreferredViewport(webApp, platform)
    },
  }
}
