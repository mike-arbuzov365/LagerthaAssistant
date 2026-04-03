import { create } from 'zustand'
import type { MiniAppPolicyResponse, PreferenceSessionResponse, SessionBootstrapResponse } from '../api/contracts'
import type { HostPlatform, HostSource } from '../host/types'
import type { AppLocale } from '../lib/locale'
import { normalizeThemeMode, type AppThemeMode, type HostTheme } from '../lib/theme'

type AppStatus = 'idle' | 'loading' | 'ready' | 'error'

interface AppHostState {
  isTelegram: boolean
  source: HostSource
  platform: HostPlatform
  theme: HostTheme
  initData: string
  userId: string | null
  conversationId: string | null
}

interface AppStore {
  status: AppStatus
  locale: AppLocale
  themeMode: AppThemeMode
  bootstrap: SessionBootstrapResponse | null
  policy: MiniAppPolicyResponse | null
  host: AppHostState | null
  error: string | null
  setLoading(): void
  setReady(payload: {
    locale: AppLocale
    bootstrap: SessionBootstrapResponse
    policy: MiniAppPolicyResponse
    host: AppHostState
  }): void
  setLocale(locale: AppLocale): void
  setThemeMode(themeMode: AppThemeMode): void
  setBootstrapPreferences(preferences: PreferenceSessionResponse): void
  setBootstrapSettings(settings: Partial<SessionBootstrapResponse['settings']>): void
  setError(message: string): void
}

export const useAppStore = create<AppStore>((set) => ({
  status: 'idle',
  locale: 'uk',
  themeMode: 'system',
  bootstrap: null,
  policy: null,
  host: null,
  error: null,
  setLoading() {
    set({ status: 'loading', error: null })
  },
  setReady(payload) {
    set({
        status: 'ready',
        locale: payload.locale,
        themeMode: normalizeThemeMode(payload.bootstrap.settings.themeMode),
        bootstrap: payload.bootstrap,
        policy: payload.policy,
        host: payload.host,
        error: null,
      })
  },
  setLocale(locale) {
    set({ locale })
  },
  setThemeMode(themeMode) {
    set({ themeMode })
  },
  setBootstrapPreferences(preferences) {
    set((state) => {
      if (!state.bootstrap) {
        return state
      }

      return {
        ...state,
        bootstrap: {
          ...state.bootstrap,
          preferences: {
            saveMode: preferences.saveMode,
            availableSaveModes: preferences.availableSaveModes,
            storageMode: preferences.storageMode,
            availableStorageModes: preferences.availableStorageModes,
          },
        },
      }
    })
  },
  setBootstrapSettings(settings) {
    set((state) => {
      if (!state.bootstrap) {
        return state
      }

      return {
        ...state,
        bootstrap: {
          ...state.bootstrap,
          settings: {
            ...state.bootstrap.settings,
            ...settings,
          },
        },
      }
    })
  },
  setError(message) {
    set({ status: 'error', error: message })
  },
}))
