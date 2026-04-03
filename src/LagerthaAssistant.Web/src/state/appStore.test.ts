import { beforeEach, describe, expect, it } from 'vitest'
import type { MiniAppPolicyResponse, SessionBootstrapResponse } from '../api/contracts'
import { useAppStore } from './appStore'

const bootstrapFixture: SessionBootstrapResponse = {
  scope: {
    channel: 'telegram',
    userId: '123',
    conversationId: '123',
  },
  locale: {
    locale: 'uk',
    availableLocales: ['uk', 'en'],
  },
  preferences: {
    saveMode: 'ask',
    availableSaveModes: ['ask', 'auto', 'off'],
    storageMode: 'graph',
    availableStorageModes: ['local', 'graph'],
  },
  policy: {
    defaultLocale: 'uk',
    supportedLocales: ['uk', 'en'],
    storageModePolicy: 'graph_only_v1',
    allowedStorageModes: ['graph'],
    oneDriveAuthScope: 'shared_provider_token_v1',
    requiresInitDataVerification: true,
    notes: ['n1'],
  },
  graph: {
    isConfigured: true,
    isAuthenticated: true,
    message: 'ok',
    accessTokenExpiresAtUtc: null,
  },
  settings: {
    aiProvider: 'openai',
    availableProviders: ['openai', 'claude'],
    aiModel: 'gpt-4.1-mini',
    availableModels: ['gpt-4.1-mini', 'gpt-4.1'],
    hasStoredKey: false,
    apiKeySource: 'missing',
    themeMode: 'system',
    availableThemeModes: ['system', 'light', 'dark'],
    notion: {
      notionVocabulary: {
        enabled: false,
        isConfigured: false,
        workerEnabled: false,
        message: 'disabled',
        pendingCards: 0,
        failedCards: 0,
      },
      notionFood: {
        enabled: false,
        isConfigured: false,
        workerEnabled: false,
        inventoryPendingOrFailed: 0,
        inventoryPermanentlyFailed: 0,
        groceryPendingOrFailed: 0,
        groceryPermanentlyFailed: 0,
      },
    },
  },
}

const policyFixture: MiniAppPolicyResponse = {
  defaultLocale: 'uk',
  supportedLocales: ['uk', 'en'],
  storageModePolicy: 'graph_only_v1',
  allowedStorageModes: ['graph'],
  oneDriveAuthScope: 'shared_provider_token_v1',
  requiresInitDataVerification: true,
  notes: ['n1'],
}

describe('appStore', () => {
  beforeEach(() => {
    useAppStore.setState({
      status: 'idle',
      locale: 'uk',
      themeMode: 'system',
      bootstrap: null,
      policy: null,
      error: null,
    })
  })

  it('stores ready payload with bootstrap and policy', () => {
    useAppStore.getState().setReady({
      locale: 'en',
      bootstrap: bootstrapFixture,
      policy: policyFixture,
    })

    const state = useAppStore.getState()
    expect(state.status).toBe('ready')
    expect(state.locale).toBe('en')
    expect(state.bootstrap?.scope.userId).toBe('123')
    expect(state.policy?.storageModePolicy).toBe('graph_only_v1')
  })

  it('updates bootstrap preferences without replacing scope', () => {
    useAppStore.getState().setReady({
      locale: 'uk',
      bootstrap: bootstrapFixture,
      policy: policyFixture,
    })

    useAppStore.getState().setBootstrapPreferences({
      saveMode: 'auto',
      availableSaveModes: ['ask', 'auto', 'off'],
      storageMode: 'graph',
      availableStorageModes: ['graph'],
    })

    const state = useAppStore.getState()
    expect(state.bootstrap?.scope.userId).toBe('123')
    expect(state.bootstrap?.preferences.saveMode).toBe('auto')
    expect(state.bootstrap?.preferences.availableStorageModes).toEqual(['graph'])
  })
})
