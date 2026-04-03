export interface SessionBootstrapRequest {
  channel?: string
  userId?: string | null
  conversationId?: string | null
  includeCommands?: boolean
  includePartOfSpeechOptions?: boolean
  includeDecks?: boolean
  initData?: string
}

export interface SessionBootstrapResponse {
  scope: {
    channel: string
    userId: string
    conversationId: string
  }
  locale: {
    locale: string
    availableLocales: string[]
  }
  preferences: {
    saveMode: string
    availableSaveModes: string[]
    storageMode: string
    availableStorageModes: string[]
  }
  policy: MiniAppPolicyResponse
  graph: {
    isConfigured: boolean
    isAuthenticated: boolean
    message: string
    accessTokenExpiresAtUtc: string | null
  }
  settings: {
    aiProvider: string
    availableProviders: string[]
    aiModel: string
    availableModels: string[]
    hasStoredKey: boolean
    apiKeySource: string
    themeMode: string
    availableThemeModes: string[]
    notion: IntegrationNotionHubStatusResponse
  }
}

export interface PreferenceSessionResponse {
  saveMode: string
  availableSaveModes: string[]
  storageMode: string
  availableStorageModes: string[]
}

export interface PreferenceSetSessionRequest {
  saveMode?: string
  storageMode?: string
  channel?: string
  userId?: string
  conversationId?: string
}

export interface MiniAppVerifyRequest {
  initData: string
}

export interface MiniAppVerifyResponse {
  isValid: boolean
  reason: string
  authDateUtc: string | null
}

export interface MiniAppDiagnosticRequest {
  sessionId: string
  eventType: string
  severity?: 'info' | 'warn' | 'error'
  message?: string
  path?: string
  isTelegram?: boolean
  hostSource?: string
  platform?: string
  channel?: string
  userId?: string | null
  conversationId?: string | null
  hasInitData?: boolean
  hasWebApp?: boolean
  locale?: string | null
  details?: Record<string, string | number | boolean | null>
}

export interface MiniAppPolicyResponse {
  defaultLocale: string
  supportedLocales: string[]
  storageModePolicy: string
  allowedStorageModes: string[]
  oneDriveAuthScope: string
  requiresInitDataVerification: boolean
  notes: string[]
}

export interface MiniAppSettingsCommitRequest {
  locale: string
  saveMode: string
  storageMode: string
  themeMode: string
  aiProvider: string
  aiModel: string
  apiKey?: string | null
  removeStoredKey?: boolean
  selectedManually?: boolean
  channel?: string
  userId?: string
  conversationId?: string
  initData?: string
}

export interface MiniAppSettingsCommitResponse {
  locale: string
  availableLocales: string[]
  saveMode: string
  availableSaveModes: string[]
  storageMode: string
  availableStorageModes: string[]
  themeMode: string
  availableThemeModes: string[]
  aiProvider: string
  availableProviders: string[]
  aiModel: string
  availableModels: string[]
  hasStoredKey: boolean
  apiKeySource: string
}

export interface IntegrationNotionHubStatusResponse {
  notionVocabulary: {
    enabled: boolean
    isConfigured: boolean
    workerEnabled: boolean
    message: string
    pendingCards: number
    failedCards: number
  }
  notionFood: {
    enabled: boolean
    isConfigured: boolean
    workerEnabled: boolean
    inventoryPendingOrFailed: number
    inventoryPermanentlyFailed: number
    groceryPendingOrFailed: number
    groceryPermanentlyFailed: number
  }
}

export interface GraphAuthStatusResponse {
  isConfigured: boolean
  isAuthenticated: boolean
  message: string
  accessTokenExpiresAtUtc: string | null
}

export interface GraphLoginResponse {
  succeeded: boolean
  message: string
  status: GraphAuthStatusResponse
}

export interface GraphDeviceLoginChallengeResponse {
  deviceCode: string
  userCode: string
  verificationUri: string
  expiresInSeconds: number
  intervalSeconds: number
  expiresAtUtc: string
  message: string | null
}

export interface GraphDeviceLoginStartResponse {
  succeeded: boolean
  message: string
  challenge: GraphDeviceLoginChallengeResponse | null
}

export interface GraphSyncNowResponse {
  succeeded: boolean
  message: string
  status: GraphAuthStatusResponse
  completed: number
  requeued: number
  failed: number
  pendingAfterRun: number
}

export interface GraphRebuildIndexResponse {
  succeeded: boolean
  message: string
  status: GraphAuthStatusResponse
  totalEntries: number
  indexedEntries: number
}

export interface GraphClearCacheResponse {
  succeeded: boolean
  message: string
  status: GraphAuthStatusResponse
  clearedEntries: number
}

export interface PreferenceLocaleResponse {
  locale: string
  availableLocales: string[]
}

export interface PreferenceSetLocaleRequest {
  locale: string
  selectedManually?: boolean
  channel?: string
  userId?: string
  conversationId?: string
}

export interface PreferenceAiProviderResponse {
  provider: string
  availableProviders: string[]
}

export interface PreferenceSetAiProviderRequest {
  provider: string
  channel?: string
  userId?: string
  conversationId?: string
}

export interface PreferenceAiModelResponse {
  provider: string
  model: string
  availableModels: string[]
}

export interface PreferenceSetAiModelRequest {
  model: string
  provider?: string
  channel?: string
  userId?: string
  conversationId?: string
}

export interface PreferenceAiKeyStatusResponse {
  provider: string
  hasStoredKey: boolean
  apiKeySource: string
}

export interface PreferenceSetAiKeyRequest {
  apiKey: string
  provider?: string
  channel?: string
  userId?: string
  conversationId?: string
}
