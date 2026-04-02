import type { HostContext, HostTheme } from './types'

declare global {
  interface Window {
    Telegram?: {
      WebApp?: {
        initData?: string
        initDataUnsafe?: {
          user?: {
            id?: number
            language_code?: string
          }
        }
        colorScheme?: 'light' | 'dark'
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

function requestBestEffortFullscreen(webApp: NonNullable<NonNullable<typeof window.Telegram>['WebApp']>) {
  webApp.expand()

  try {
    webApp.requestFullscreen?.()
  } catch {
    // Older Telegram clients may not support fullscreen requests.
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

  return {
    isTelegram: true,
    theme: resolveTheme(webApp.colorScheme),
    safeAreaTop: Math.max(0, webApp.contentSafeAreaInsets?.top ?? 0),
    initData: webApp.initData ?? '',
    userLanguageCode,
    userId,
    conversationId: userId,
    ready() {
      webApp.ready()
      requestBestEffortFullscreen(webApp)
    },
    enableBestEffortFullscreen() {
      let disposed = false
      let fullscreenAttempted = false

      const tryRequest = () => {
        if (disposed || fullscreenAttempted) {
          return
        }

        fullscreenAttempted = true
        requestBestEffortFullscreen(webApp)
        cleanup()
      }

      const cleanup = () => {
        disposed = true
        window.removeEventListener('pointerdown', tryRequest, true)
        window.removeEventListener('touchstart', tryRequest, true)
        window.removeEventListener('keydown', tryRequest, true)
        window.removeEventListener('scroll', tryRequest, true)
        webApp.offEvent?.('fullscreenChanged', tryRequest)
      }

      window.addEventListener('pointerdown', tryRequest, { capture: true, passive: true })
      window.addEventListener('touchstart', tryRequest, { capture: true, passive: true })
      window.addEventListener('keydown', tryRequest, true)
      window.addEventListener('scroll', tryRequest, { capture: true, passive: true })

      // If Telegram changes viewport state while the app is opening, try to maximize again.
      webApp.onEvent?.('fullscreenChanged', tryRequest)

      return cleanup
    },
  }
}
