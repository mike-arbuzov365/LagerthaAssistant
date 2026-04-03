import type {
  GraphAuthStatusResponse,
  GraphClearCacheResponse,
  GraphDeviceLoginChallengeResponse,
  GraphDeviceLoginStartResponse,
  GraphLoginResponse,
  GraphRebuildIndexResponse,
  GraphSyncNowResponse,
  IntegrationNotionHubStatusResponse,
  MiniAppDiagnosticRequest,
  MiniAppPolicyResponse,
  MiniAppSettingsCommitRequest,
  MiniAppSettingsCommitResponse,
  PreferenceSessionResponse,
  PreferenceSetSessionRequest,
  PreferenceAiKeyStatusResponse,
  PreferenceAiModelResponse,
  PreferenceAiProviderResponse,
  PreferenceLocaleResponse,
  PreferenceSetAiKeyRequest,
  PreferenceSetAiModelRequest,
  PreferenceSetAiProviderRequest,
  PreferenceSetLocaleRequest,
  MiniAppVerifyRequest,
  MiniAppVerifyResponse,
  SessionBootstrapRequest,
  SessionBootstrapResponse,
} from './contracts'

const JSON_HEADERS = {
  'Content-Type': 'application/json',
}

export async function verifyMiniAppInitData(request: MiniAppVerifyRequest): Promise<MiniAppVerifyResponse> {
  const response = await fetch('/api/miniapp/auth/verify', {
    method: 'POST',
    headers: JSON_HEADERS,
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw new Error(`Init data verify failed: ${response.status}`)
  }

  return response.json() as Promise<MiniAppVerifyResponse>
}

export async function getSessionBootstrap(
  request: SessionBootstrapRequest,
): Promise<SessionBootstrapResponse> {
  const response = await fetch('/api/session/bootstrap', {
    method: 'POST',
    headers: JSON_HEADERS,
    body: JSON.stringify({
      channel: 'telegram',
      includeCommands: false,
      includePartOfSpeechOptions: false,
      includeDecks: false,
      ...request,
    }),
  })
  if (!response.ok) {
    throw new Error(`Bootstrap failed: ${response.status}`)
  }

  return response.json() as Promise<SessionBootstrapResponse>
}

export async function getMiniAppPolicy(): Promise<MiniAppPolicyResponse> {
  const response = await fetch('/api/miniapp/policy')
  if (!response.ok) {
    throw new Error(`Mini App policy load failed: ${response.status}`)
  }

  return response.json() as Promise<MiniAppPolicyResponse>
}

export async function commitMiniAppSettings(
  request: MiniAppSettingsCommitRequest,
): Promise<MiniAppSettingsCommitResponse> {
  const response = await fetch('/api/miniapp/settings/commit', {
    method: 'POST',
    headers: JSON_HEADERS,
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw new Error(`Mini App settings commit failed: ${response.status}`)
  }

  return response.json() as Promise<MiniAppSettingsCommitResponse>
}

export async function postMiniAppDiagnostic(request: MiniAppDiagnosticRequest): Promise<void> {
  const response = await fetch('/api/miniapp/diagnostics', {
    method: 'POST',
    headers: JSON_HEADERS,
    body: JSON.stringify(request),
    keepalive: true,
  })

  if (!response.ok) {
    throw new Error(`Mini App diagnostics failed: ${response.status}`)
  }
}

export async function getLocale(
  userId: string | null,
  conversationId?: string | null,
): Promise<PreferenceLocaleResponse> {
  const params = new URLSearchParams({ channel: 'telegram' })
  if (userId) {
    params.set('userId', userId)
  }
  if (conversationId) {
    params.set('conversationId', conversationId)
  }

  const response = await fetch(`/api/preferences/locale?${params.toString()}`)
  if (!response.ok) {
    throw new Error(`Locale load failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceLocaleResponse>
}

export async function setLocale(request: PreferenceSetLocaleRequest): Promise<PreferenceLocaleResponse> {
  const response = await fetch('/api/preferences/locale', {
    method: 'PUT',
    headers: JSON_HEADERS,
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw new Error(`Locale update failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceLocaleResponse>
}

export async function getAiProvider(
  userId: string | null,
  conversationId?: string | null,
): Promise<PreferenceAiProviderResponse> {
  const params = new URLSearchParams({ channel: 'telegram' })
  if (userId) {
    params.set('userId', userId)
  }
  if (conversationId) {
    params.set('conversationId', conversationId)
  }

  const response = await fetch(`/api/preferences/ai/provider?${params.toString()}`)
  if (!response.ok) {
    throw new Error(`AI provider load failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceAiProviderResponse>
}

export async function setAiProvider(request: PreferenceSetAiProviderRequest): Promise<PreferenceAiProviderResponse> {
  const response = await fetch('/api/preferences/ai/provider', {
    method: 'PUT',
    headers: JSON_HEADERS,
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw new Error(`AI provider update failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceAiProviderResponse>
}

export async function getAiModel(
  userId: string | null,
  provider?: string,
  conversationId?: string | null,
): Promise<PreferenceAiModelResponse> {
  const params = new URLSearchParams({ channel: 'telegram' })
  if (userId) {
    params.set('userId', userId)
  }
  if (conversationId) {
    params.set('conversationId', conversationId)
  }
  if (provider) {
    params.set('provider', provider)
  }

  const response = await fetch(`/api/preferences/ai/model?${params.toString()}`)
  if (!response.ok) {
    throw new Error(`AI model load failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceAiModelResponse>
}

export async function setAiModel(request: PreferenceSetAiModelRequest): Promise<PreferenceAiModelResponse> {
  const response = await fetch('/api/preferences/ai/model', {
    method: 'PUT',
    headers: JSON_HEADERS,
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw new Error(`AI model update failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceAiModelResponse>
}

export async function getAiKeyStatus(
  userId: string | null,
  provider?: string,
  conversationId?: string | null,
): Promise<PreferenceAiKeyStatusResponse> {
  const params = new URLSearchParams({ channel: 'telegram' })
  if (userId) {
    params.set('userId', userId)
  }
  if (conversationId) {
    params.set('conversationId', conversationId)
  }
  if (provider) {
    params.set('provider', provider)
  }

  const response = await fetch(`/api/preferences/ai/key/status?${params.toString()}`)
  if (!response.ok) {
    throw new Error(`AI key status load failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceAiKeyStatusResponse>
}

export async function setAiKey(request: PreferenceSetAiKeyRequest): Promise<PreferenceAiKeyStatusResponse> {
  const response = await fetch('/api/preferences/ai/key', {
    method: 'POST',
    headers: JSON_HEADERS,
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw new Error(`AI key update failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceAiKeyStatusResponse>
}

export async function removeAiKey(
  userId: string | null,
  provider?: string,
): Promise<PreferenceAiKeyStatusResponse> {
  const params = new URLSearchParams({ channel: 'telegram' })
  if (userId) {
    params.set('userId', userId)
  }
  if (provider) {
    params.set('provider', provider)
  }

  const response = await fetch(`/api/preferences/ai/key?${params.toString()}`, {
    method: 'DELETE',
  })
  if (!response.ok) {
    throw new Error(`AI key remove failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceAiKeyStatusResponse>
}

export async function setPreferenceSession(request: PreferenceSetSessionRequest): Promise<PreferenceSessionResponse> {
  const response = await fetch('/api/preferences/session', {
    method: 'PUT',
    headers: JSON_HEADERS,
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw new Error(`Session preferences update failed: ${response.status}`)
  }

  return response.json() as Promise<PreferenceSessionResponse>
}

export async function getIntegrationNotionStatus(): Promise<IntegrationNotionHubStatusResponse> {
  const response = await fetch('/api/integrations/notion/status')
  if (!response.ok) {
    throw new Error(`Integrations status load failed: ${response.status}`)
  }

  return response.json() as Promise<IntegrationNotionHubStatusResponse>
}

export async function getGraphStatus(): Promise<GraphAuthStatusResponse> {
  const response = await fetch('/api/graph/status')
  if (!response.ok) {
    throw new Error(`Graph status load failed: ${response.status}`)
  }

  return response.json() as Promise<GraphAuthStatusResponse>
}

export async function startGraphLogin(): Promise<GraphDeviceLoginStartResponse> {
  const response = await fetch('/api/graph/login/start', { method: 'POST' })
  if (!response.ok) {
    throw new Error(`Graph login start failed: ${response.status}`)
  }

  return response.json() as Promise<GraphDeviceLoginStartResponse>
}

export async function completeGraphLogin(challenge: GraphDeviceLoginChallengeResponse): Promise<GraphLoginResponse> {
  const response = await fetch('/api/graph/login/complete', {
    method: 'POST',
    headers: JSON_HEADERS,
    body: JSON.stringify({ challenge }),
  })

  if (!response.ok) {
    throw new Error(`Graph login complete failed: ${response.status}`)
  }

  return response.json() as Promise<GraphLoginResponse>
}

export async function graphLogout(): Promise<GraphAuthStatusResponse> {
  const response = await fetch('/api/graph/logout', { method: 'POST' })
  if (!response.ok) {
    throw new Error(`Graph logout failed: ${response.status}`)
  }

  return response.json() as Promise<GraphAuthStatusResponse>
}

export async function graphSyncNow(): Promise<GraphSyncNowResponse> {
  const response = await fetch('/api/graph/sync-now', { method: 'POST' })
  if (!response.ok) {
    throw new Error(`Graph sync failed: ${response.status}`)
  }

  return response.json() as Promise<GraphSyncNowResponse>
}

export async function graphRebuildIndex(): Promise<GraphRebuildIndexResponse> {
  const response = await fetch('/api/graph/rebuild-index', { method: 'POST' })
  if (!response.ok) {
    throw new Error(`Graph rebuild index failed: ${response.status}`)
  }

  return response.json() as Promise<GraphRebuildIndexResponse>
}

export async function graphClearCache(): Promise<GraphClearCacheResponse> {
  const response = await fetch('/api/graph/clear-cache', { method: 'POST' })
  if (!response.ok) {
    throw new Error(`Graph clear cache failed: ${response.status}`)
  }

  return response.json() as Promise<GraphClearCacheResponse>
}
