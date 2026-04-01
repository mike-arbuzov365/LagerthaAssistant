import { create } from 'zustand'
import type { MiniAppPolicyResponse, PreferenceSessionResponse, SessionBootstrapResponse } from '../api/contracts'
import type { AppLocale } from '../lib/locale'

type AppStatus = 'idle' | 'loading' | 'ready' | 'error'

interface AppStore {
  status: AppStatus
  locale: AppLocale
  bootstrap: SessionBootstrapResponse | null
  policy: MiniAppPolicyResponse | null
  error: string | null
  setLoading(): void
  setReady(payload: { locale: AppLocale; bootstrap: SessionBootstrapResponse; policy: MiniAppPolicyResponse }): void
  setLocale(locale: AppLocale): void
  setBootstrapPreferences(preferences: PreferenceSessionResponse): void
  setError(message: string): void
}

export const useAppStore = create<AppStore>((set) => ({
  status: 'idle',
  locale: 'uk',
  bootstrap: null,
  policy: null,
  error: null,
  setLoading() {
    set({ status: 'loading', error: null })
  },
  setReady(payload) {
    set({
      status: 'ready',
      locale: payload.locale,
      bootstrap: payload.bootstrap,
      policy: payload.policy,
      error: null,
    })
  },
  setLocale(locale) {
    set({ locale })
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
  setError(message) {
    set({ status: 'error', error: message })
  },
}))
