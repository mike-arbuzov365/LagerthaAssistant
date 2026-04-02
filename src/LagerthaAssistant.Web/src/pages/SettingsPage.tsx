import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  completeGraphLogin,
  getAiKeyStatus,
  getAiModel,
  getAiProvider,
  getGraphStatus,
  getIntegrationNotionStatus,
  getLocale,
  getSessionBootstrap,
  graphClearCache,
  graphLogout,
  graphRebuildIndex,
  graphSyncNow,
  removeAiKey,
  setAiKey,
  setAiModel,
  setAiProvider,
  setLocale,
  setPreferenceSession,
  startGraphLogin,
} from '../api/client'
import type { GraphDeviceLoginChallengeResponse } from '../api/contracts'
import type { AppLocale } from '../lib/locale'
import { getScopedUserId } from '../lib/settings-utils'
import { useAppStore } from '../state/appStore'

type BannerTone = 'ok' | 'error' | 'warn'

interface CopyPack {
  screenTitle: string
  screenSubtitle: string
  online: string
  offline: string
  noBootstrap: string
  loadingAi: string
  loadingIntegrations: string
  loadingModels: string
  retry: string
  generalSection: string
  aiSection: string
  integrationsSection: string
  languageLabel: string
  saveModeLabel: string
  storageModeLabel: string
  providerLabel: string
  modelLabel: string
  apiKeyLabel: string
  apiKeyPlaceholder: string
  removeStoredKeyLabel: string
  keySourceLabel: string
  keyStoredLabel: string
  modelCountLabel: string
  oneDriveStatusLabel: string
  oneDriveTokenLabel: string
  notionLabel: string
  connected: string
  disconnected: string
  enabled: string
  disabled: string
  yes: string
  no: string
  notConfigured: string
  noData: string
  saveChanges: string
  saving: string
  unsavedChanges: string
  allSaved: string
  noChanges: string
  saveSuccess: string
  offlineError: string
  errorPrefix: string
  loadErrorPrefix: string
  refreshStatus: string
  startLogin: string
  finishLogin: string
  logout: string
  serviceActions: string
  syncNow: string
  rebuildIndex: string
  clearCache: string
  loginCodeLabel: string
  openLoginPage: string
  enterCodeFirst: string
  storageLockedHint: string
}

interface PersistedSnapshot {
  locale: AppLocale
  saveMode: string
  storageMode: string
  aiProvider: string
  aiModel: string
}

const copyByLocale: Record<AppLocale, CopyPack> = {
  uk: {
    screenTitle: 'Налаштування Lagertha',
    screenSubtitle: 'Єдиний екран для мови, AI та інтеграцій.',
    online: 'Онлайн',
    offline: 'Офлайн',
    noBootstrap: 'Немає даних bootstrap.',
    loadingAi: 'Завантаження AI-налаштувань…',
    loadingIntegrations: 'Завантаження статусів інтеграцій…',
    loadingModels: 'Оновлюємо список моделей…',
    retry: 'Спробувати знову',
    generalSection: 'Загальні',
    aiSection: 'AI',
    integrationsSection: 'Інтеграції',
    languageLabel: 'Мова інтерфейсу',
    saveModeLabel: 'Режим збереження',
    storageModeLabel: 'Режим сховища',
    providerLabel: 'Провайдер',
    modelLabel: 'Модель',
    apiKeyLabel: 'API ключ',
    apiKeyPlaceholder: 'Введіть новий ключ (необовʼязково)',
    removeStoredKeyLabel: 'Видалити збережений ключ при збереженні',
    keySourceLabel: 'Джерело ключа',
    keyStoredLabel: 'Ключ у сховищі',
    modelCountLabel: 'Доступно моделей',
    oneDriveStatusLabel: 'OneDrive / Graph',
    oneDriveTokenLabel: 'Токен до',
    notionLabel: 'Notion',
    connected: 'Підключено',
    disconnected: 'Не підключено',
    enabled: 'Увімкнено',
    disabled: 'Вимкнено',
    yes: 'Так',
    no: 'Ні',
    notConfigured: 'Не налаштовано',
    noData: 'Немає',
    saveChanges: 'Зберегти зміни',
    saving: 'Збереження…',
    unsavedChanges: 'Є незбережені зміни',
    allSaved: 'Усі зміни збережено',
    noChanges: 'Немає нових змін для збереження.',
    saveSuccess: 'Налаштування збережено.',
    offlineError: 'Немає мережі. Збереження недоступне.',
    errorPrefix: 'Помилка',
    loadErrorPrefix: 'Не вдалося завантажити дані',
    refreshStatus: 'Оновити статус',
    startLogin: 'Почати вхід',
    finishLogin: 'Завершити вхід',
    logout: 'Вийти',
    serviceActions: 'Сервісні дії',
    syncNow: 'Синхронізувати зараз',
    rebuildIndex: 'Перебудувати індекс',
    clearCache: 'Очистити кеш',
    loginCodeLabel: 'Код входу',
    openLoginPage: 'Відкрити сторінку входу',
    enterCodeFirst: 'Спочатку ініціюйте вхід у OneDrive.',
    storageLockedHint: 'Режим сховища обмежено policy Wave 1.',
  },
  en: {
    screenTitle: 'Lagertha Settings',
    screenSubtitle: 'One screen for language, AI, and integrations.',
    online: 'Online',
    offline: 'Offline',
    noBootstrap: 'No bootstrap data available.',
    loadingAi: 'Loading AI settings…',
    loadingIntegrations: 'Loading integration statuses…',
    loadingModels: 'Updating model list…',
    retry: 'Retry',
    generalSection: 'General',
    aiSection: 'AI',
    integrationsSection: 'Integrations',
    languageLabel: 'Interface language',
    saveModeLabel: 'Save mode',
    storageModeLabel: 'Storage mode',
    providerLabel: 'Provider',
    modelLabel: 'Model',
    apiKeyLabel: 'API key',
    apiKeyPlaceholder: 'Enter a new key (optional)',
    removeStoredKeyLabel: 'Remove stored key on save',
    keySourceLabel: 'Key source',
    keyStoredLabel: 'Key in storage',
    modelCountLabel: 'Models available',
    oneDriveStatusLabel: 'OneDrive / Graph',
    oneDriveTokenLabel: 'Token valid until',
    notionLabel: 'Notion',
    connected: 'Connected',
    disconnected: 'Disconnected',
    enabled: 'Enabled',
    disabled: 'Disabled',
    yes: 'Yes',
    no: 'No',
    notConfigured: 'Not configured',
    noData: 'None',
    saveChanges: 'Save changes',
    saving: 'Saving…',
    unsavedChanges: 'You have unsaved changes',
    allSaved: 'All changes saved',
    noChanges: 'No new changes to save.',
    saveSuccess: 'Settings saved.',
    offlineError: 'No network. Saving is unavailable.',
    errorPrefix: 'Error',
    loadErrorPrefix: 'Failed to load data',
    refreshStatus: 'Refresh status',
    startLogin: 'Start login',
    finishLogin: 'Complete login',
    logout: 'Sign out',
    serviceActions: 'Service actions',
    syncNow: 'Sync now',
    rebuildIndex: 'Rebuild index',
    clearCache: 'Clear cache',
    loginCodeLabel: 'Login code',
    openLoginPage: 'Open login page',
    enterCodeFirst: 'Start OneDrive login first.',
    storageLockedHint: 'Storage mode is locked by Wave 1 policy.',
  },
}

function mapSaveMode(mode: string, locale: AppLocale): string {
  const labels = {
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
  }

  return labels[locale][mode as keyof typeof labels.uk] ?? mode
}

function mapStorageMode(mode: string, locale: AppLocale): string {
  const labels = {
    uk: {
      graph: 'Graph (OneDrive)',
      local: 'Локальне',
    },
    en: {
      graph: 'Graph (OneDrive)',
      local: 'Local',
    },
  }

  return labels[locale][mode as keyof typeof labels.uk] ?? mode
}

function mapAiKeySource(source: string, locale: AppLocale): string {
  const labels = {
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
  }

  return labels[locale][source as keyof typeof labels.uk] ?? source
}

function formatDateTime(value: string | null, locale: AppLocale, emptyLabel: string): string {
  if (!value) {
    return emptyLabel
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return new Intl.DateTimeFormat(locale === 'uk' ? 'uk-UA' : 'en-US', {
    dateStyle: 'short',
    timeStyle: 'short',
    hour12: false,
  }).format(date)
}

function resolveBannerTone(value: string): BannerTone {
  if (value.startsWith('Помилка') || value.startsWith('Error')) {
    return 'error'
  }

  if (value.startsWith('Увага') || value.startsWith('Warning')) {
    return 'warn'
  }

  return 'ok'
}

function StatusChip({ tone, children }: { tone: BannerTone; children: string }) {
  return <span className={`chip chip--${tone}`}>{children}</span>
}

type TelegramWebAppWithClosingConfirmation = {
  enableClosingConfirmation?: () => void
  disableClosingConfirmation?: () => void
  isClosingConfirmationEnabled?: boolean
}

function applyTelegramClosingConfirmation(
  webApp: TelegramWebAppWithClosingConfirmation | undefined,
  enabled: boolean,
) {
  if (!webApp) {
    return
  }

  if (enabled) {
    webApp.enableClosingConfirmation?.()
  } else {
    webApp.disableClosingConfirmation?.()
  }

  // Compatibility fallback for Telegram clients that expose only flag-based behavior.
  try {
    webApp.isClosingConfirmationEnabled = enabled
  } catch {
    // no-op
  }
}

export function SettingsPage() {
  const locale = useAppStore((s) => s.locale)
  const bootstrap = useAppStore((s) => s.bootstrap)
  const policy = useAppStore((s) => s.policy)
  const setLocaleInStore = useAppStore((s) => s.setLocale)
  const setBootstrapPreferences = useAppStore((s) => s.setBootstrapPreferences)

  const scopedUserId = useMemo(
    () => getScopedUserId(bootstrap?.scope.userId),
    [bootstrap?.scope.userId],
  )

  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [saveStatus, setSaveStatus] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [isOnline, setIsOnline] = useState(() => (typeof navigator === 'undefined' ? true : navigator.onLine))

  const [localeDraft, setLocaleDraft] = useState<AppLocale>(locale)
  const [saveModeDraft, setSaveModeDraft] = useState('ask')
  const [storageModeDraft, setStorageModeDraft] = useState('graph')

  const [aiProviderDraft, setAiProviderDraft] = useState('openai')
  const [aiProviders, setAiProviders] = useState<string[]>([])
  const [aiModelDraft, setAiModelDraft] = useState('')
  const [aiModels, setAiModels] = useState<string[]>([])
  const [modelsLoading, setModelsLoading] = useState(false)

  const [apiKeyDraft, setApiKeyDraft] = useState('')
  const [removeStoredKeyRequested, setRemoveStoredKeyRequested] = useState(false)

  const [integrationStatus, setIntegrationStatus] = useState<Awaited<ReturnType<typeof getIntegrationNotionStatus>> | null>(null)
  const [graphStatus, setGraphStatus] = useState(bootstrap?.graph ?? null)
  const [loginChallenge, setLoginChallenge] = useState<GraphDeviceLoginChallengeResponse | null>(null)
  const [keyStatus, setKeyStatus] = useState<{ hasStoredKey: boolean; source: string }>({
    hasStoredKey: false,
    source: 'missing',
  })
  const [snapshot, setSnapshot] = useState<PersistedSnapshot | null>(null)

  const providerRequestVersion = useRef(0)

  useEffect(() => {
    setLocaleDraft(locale)
  }, [locale])

  useEffect(() => {
    function handleOnline() {
      setIsOnline(true)
    }

    function handleOffline() {
      setIsOnline(false)
    }

    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)

    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [])

  const uiLocale = localeDraft
  const copy = copyByLocale[uiLocale]

  const hasUnsavedChanges = useMemo(() => {
    if (!snapshot) {
      return false
    }

    return (
      localeDraft !== snapshot.locale
      || saveModeDraft !== snapshot.saveMode
      || storageModeDraft !== snapshot.storageMode
      || aiProviderDraft !== snapshot.aiProvider
      || aiModelDraft !== snapshot.aiModel
      || removeStoredKeyRequested
      || apiKeyDraft.trim().length > 0
    )
  }, [
    aiModelDraft,
    aiProviderDraft,
    apiKeyDraft,
    localeDraft,
    removeStoredKeyRequested,
    saveModeDraft,
    snapshot,
    storageModeDraft,
  ])

  useEffect(() => {
    if (!hasUnsavedChanges) {
      return
    }

    function beforeUnloadHandler(event: BeforeUnloadEvent) {
      event.preventDefault()
      event.returnValue = ''
    }

    window.addEventListener('beforeunload', beforeUnloadHandler)

    return () => {
      window.removeEventListener('beforeunload', beforeUnloadHandler)
    }
  }, [hasUnsavedChanges])

  useEffect(() => {
    const webApp = window.Telegram?.WebApp as TelegramWebAppWithClosingConfirmation | undefined

    if (!webApp) {
      return
    }

    applyTelegramClosingConfirmation(webApp, hasUnsavedChanges)

    return () => {
      applyTelegramClosingConfirmation(webApp, false)
    }
  }, [hasUnsavedChanges])

  const loadSettings = useCallback(async () => {
    if (!bootstrap) {
      return
    }

    setLoading(true)
    setLoadError(null)

    try {
      const providerResponse = await getAiProvider(scopedUserId)
      const [sessionBootstrapResponse, localeResponse, modelResponse, keyResponse, notionHubStatus, graphStatusResponse] = await Promise.all([
        getSessionBootstrap(scopedUserId),
        getLocale(scopedUserId),
        getAiModel(scopedUserId, providerResponse.provider),
        getAiKeyStatus(scopedUserId, providerResponse.provider),
        getIntegrationNotionStatus(),
        getGraphStatus(),
      ])

      setAiProviderDraft(providerResponse.provider)
      setAiProviders(providerResponse.availableProviders)
      setAiModelDraft(modelResponse.model)
      setAiModels(modelResponse.availableModels)
      setKeyStatus({
        hasStoredKey: keyResponse.hasStoredKey,
        source: keyResponse.apiKeySource,
      })
      setIntegrationStatus(notionHubStatus)
      setGraphStatus(graphStatusResponse)
      setLoginChallenge(null)
      setRemoveStoredKeyRequested(false)
      setApiKeyDraft('')

      const normalizedLocale: AppLocale = localeResponse.locale === 'en' ? 'en' : 'uk'
      const saveModeFromApi = sessionBootstrapResponse.preferences.saveMode
      const storageModeFromApi = sessionBootstrapResponse.preferences.storageMode

      setLocaleInStore(normalizedLocale)
      setLocaleDraft(normalizedLocale)
      setSaveModeDraft(saveModeFromApi)
      setStorageModeDraft(storageModeFromApi)
      setSnapshot({
        locale: normalizedLocale,
        saveMode: saveModeFromApi,
        storageMode: storageModeFromApi,
        aiProvider: providerResponse.provider,
        aiModel: modelResponse.model,
      })
    } catch (error) {
      setLoadError(error instanceof Error ? error.message : 'Load failed')
    } finally {
      setLoading(false)
    }
  }, [bootstrap, scopedUserId, setLocaleInStore])

  useEffect(() => {
    if (!bootstrap) {
      return
    }

    setGraphStatus(bootstrap.graph)
    void loadSettings()
  }, [bootstrap, loadSettings])

  useEffect(() => {
    if (!bootstrap || !aiProviderDraft) {
      return
    }

    const requestVersion = ++providerRequestVersion.current
    setModelsLoading(true)

    void (async () => {
      try {
        const [modelResponse, keyResponse] = await Promise.all([
          getAiModel(scopedUserId, aiProviderDraft),
          getAiKeyStatus(scopedUserId, aiProviderDraft),
        ])

        if (requestVersion !== providerRequestVersion.current) {
          return
        }

        setAiModels(modelResponse.availableModels)
        setAiModelDraft((currentModel) => (
          modelResponse.availableModels.includes(currentModel)
            ? currentModel
            : modelResponse.model
        ))
        setKeyStatus({
          hasStoredKey: keyResponse.hasStoredKey,
          source: keyResponse.apiKeySource,
        })
      } catch (error) {
        if (requestVersion !== providerRequestVersion.current) {
          return
        }

        const message = error instanceof Error ? error.message : 'Model list update failed'
        setSaveStatus(`Error: ${message}`)
      } finally {
        if (requestVersion === providerRequestVersion.current) {
          setModelsLoading(false)
        }
      }
    })()
  }, [aiProviderDraft, bootstrap, scopedUserId])

  if (!bootstrap || !policy) {
    return <div className="card">{copy.noBootstrap}</div>
  }

  const activeGraphStatus = graphStatus ?? bootstrap.graph
  const storageLocked = policy.allowedStorageModes.length === 1
  const storageOptions = storageLocked
    ? policy.allowedStorageModes
    : bootstrap.preferences.availableStorageModes

  const oneDriveConnected = activeGraphStatus.isAuthenticated
  const notionEnabled = integrationStatus?.notionVocabulary.enabled || integrationStatus?.notionFood.enabled

  async function runAction(task: () => Promise<void>, successMessage: string) {
    if (!isOnline) {
      setSaveStatus(copy.offlineError)
      return
    }

    setSaving(true)
    setSaveStatus(null)

    try {
      await task()
      setSaveStatus(successMessage)
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Operation failed'
      setSaveStatus(`${copy.errorPrefix}: ${message}`)
    } finally {
      setSaving(false)
    }
  }

  async function handleSaveAll() {
    if (!snapshot) {
      return
    }

    if (!hasUnsavedChanges) {
      setSaveStatus(copy.noChanges)
      return
    }

    await runAction(async () => {
      let activeProvider = aiProviderDraft

      if (localeDraft !== snapshot.locale) {
        const localeResponse = await setLocale({
          locale: localeDraft,
          selectedManually: true,
          channel: 'telegram',
          userId: scopedUserId ?? undefined,
        })

        const normalizedLocale: AppLocale = localeResponse.locale === 'en' ? 'en' : 'uk'
        setLocaleInStore(normalizedLocale)
      }

      if (
        saveModeDraft !== snapshot.saveMode
        || storageModeDraft !== snapshot.storageMode
      ) {
        const sessionResponse = await setPreferenceSession({
          saveMode: saveModeDraft,
          storageMode: storageModeDraft,
          channel: 'telegram',
          userId: scopedUserId ?? undefined,
        })

        setBootstrapPreferences(sessionResponse)
      }

      if (aiProviderDraft !== snapshot.aiProvider) {
        const providerResponse = await setAiProvider({
          provider: aiProviderDraft,
          channel: 'telegram',
          userId: scopedUserId ?? undefined,
        })

        activeProvider = providerResponse.provider
        setAiProviders(providerResponse.availableProviders)
      }

      if (aiProviderDraft !== snapshot.aiProvider || aiModelDraft !== snapshot.aiModel) {
        const modelResponse = await setAiModel({
          provider: activeProvider,
          model: aiModelDraft,
          channel: 'telegram',
          userId: scopedUserId ?? undefined,
        })

        activeProvider = modelResponse.provider
        setAiModelDraft(modelResponse.model)
        setAiModels(modelResponse.availableModels)
      }

      if (removeStoredKeyRequested) {
        const keyResponse = await removeAiKey(scopedUserId, activeProvider)
        setKeyStatus({
          hasStoredKey: keyResponse.hasStoredKey,
          source: keyResponse.apiKeySource,
        })
      } else if (apiKeyDraft.trim().length > 0) {
        const keyResponse = await setAiKey({
          provider: activeProvider,
          apiKey: apiKeyDraft.trim(),
          channel: 'telegram',
          userId: scopedUserId ?? undefined,
        })

        setKeyStatus({
          hasStoredKey: keyResponse.hasStoredKey,
          source: keyResponse.apiKeySource,
        })
      }

      await loadSettings()
    }, copy.saveSuccess)
  }

  async function handleRefreshStatus() {
    await runAction(async () => {
      const [notionStatus, currentGraphStatus] = await Promise.all([
        getIntegrationNotionStatus(),
        getGraphStatus(),
      ])
      setIntegrationStatus(notionStatus)
      setGraphStatus(currentGraphStatus)
    }, copy.saveSuccess)
  }

  async function handleStartOneDriveLogin() {
    await runAction(async () => {
      const response = await startGraphLogin()
      if (!response.succeeded || !response.challenge) {
        throw new Error(response.message)
      }
      setLoginChallenge(response.challenge)
    }, copy.saveSuccess)
  }

  async function handleCompleteOneDriveLogin() {
    if (!loginChallenge) {
      setSaveStatus(`${copy.errorPrefix}: ${copy.enterCodeFirst}`)
      return
    }

    await runAction(async () => {
      const response = await completeGraphLogin(loginChallenge)
      setGraphStatus(response.status)
      if (!response.succeeded) {
        throw new Error(response.message)
      }
      setLoginChallenge(null)
    }, copy.saveSuccess)
  }

  async function handleLogoutOneDrive() {
    await runAction(async () => {
      const status = await graphLogout()
      setGraphStatus(status)
      setLoginChallenge(null)
    }, copy.saveSuccess)
  }

  async function handleSyncNow() {
    await runAction(async () => {
      await graphSyncNow()
    }, copy.saveSuccess)
  }

  async function handleRebuildIndex() {
    await runAction(async () => {
      await graphRebuildIndex()
    }, copy.saveSuccess)
  }

  async function handleClearCache() {
    await runAction(async () => {
      await graphClearCache()
    }, copy.saveSuccess)
  }

  return (
    <div className="settings-page" aria-busy={saving || loading}>
      <section className="tg-profile-card">
        <div className="tg-profile-main">
          <div className="tg-avatar" aria-hidden="true">⚙️</div>
          <div>
            <h2 className="tg-profile-title">{copy.screenTitle}</h2>
            <p className="tg-profile-subtitle">{copy.screenSubtitle}</p>
          </div>
        </div>
        <StatusChip tone={isOnline ? 'ok' : 'warn'}>{isOnline ? copy.online : copy.offline}</StatusChip>
      </section>

      {saveStatus && (
        <section
          className={`status-banner status-banner--${resolveBannerTone(saveStatus)}`}
          role={saveStatus.startsWith(copy.errorPrefix) ? 'alert' : 'status'}
          aria-live="polite"
        >
          {saveStatus}
        </section>
      )}

      {loadError && (
        <section className="status-banner status-banner--error" role="alert">
          {copy.loadErrorPrefix}: {loadError}
          <button type="button" className="btn-secondary" onClick={() => void loadSettings()} disabled={saving}>
            {copy.retry}
          </button>
        </section>
      )}

      <section className="tg-card">
        <h3 className="tg-section-title">{copy.generalSection}</h3>
        <div className="tg-list">
          <label className="tg-row" htmlFor="locale-select">
            <span className="tg-row-label">{copy.languageLabel}</span>
            <select
              id="locale-select"
              className="tg-select"
              value={localeDraft}
              onChange={(event) => setLocaleDraft(event.target.value === 'en' ? 'en' : 'uk')}
              disabled={saving || !isOnline}
            >
              <option value="uk">Українська</option>
              <option value="en">English</option>
            </select>
          </label>

          <label className="tg-row" htmlFor="save-mode-select">
            <span className="tg-row-label">{copy.saveModeLabel}</span>
            <select
              id="save-mode-select"
              className="tg-select"
              value={saveModeDraft}
              onChange={(event) => setSaveModeDraft(event.target.value)}
              disabled={saving || !isOnline}
            >
              {bootstrap.preferences.availableSaveModes.map((mode) => (
                <option key={mode} value={mode}>
                  {mapSaveMode(mode, uiLocale)}
                </option>
              ))}
            </select>
          </label>

          <label className="tg-row" htmlFor="storage-mode-select">
            <span className="tg-row-label">{copy.storageModeLabel}</span>
            <select
              id="storage-mode-select"
              className="tg-select"
              value={storageModeDraft}
              onChange={(event) => setStorageModeDraft(event.target.value)}
              disabled={saving || !isOnline || storageLocked}
            >
              {storageOptions.map((mode) => (
                <option key={mode} value={mode}>
                  {mapStorageMode(mode, uiLocale)}
                </option>
              ))}
            </select>
          </label>
        </div>
        {storageLocked && <p className="tg-helper">{copy.storageLockedHint}</p>}
      </section>

      <section className="tg-card">
        <h3 className="tg-section-title">{copy.aiSection}</h3>
        {loading && <p className="tg-helper">{copy.loadingAi}</p>}

        {!loading && (
          <>
            <div className="tg-list">
              <label className="tg-row" htmlFor="provider-select">
                <span className="tg-row-label">{copy.providerLabel}</span>
                <select
                  id="provider-select"
                  className="tg-select"
                  value={aiProviderDraft}
                  onChange={(event) => setAiProviderDraft(event.target.value)}
                  disabled={saving || !isOnline}
                >
                  {aiProviders.map((provider) => (
                    <option key={provider} value={provider}>
                      {provider}
                    </option>
                  ))}
                </select>
              </label>

              <label className="tg-row" htmlFor="model-select">
                <span className="tg-row-label">{copy.modelLabel}</span>
                <select
                  id="model-select"
                  className="tg-select"
                  value={aiModelDraft}
                  onChange={(event) => setAiModelDraft(event.target.value)}
                  disabled={saving || !isOnline || modelsLoading}
                >
                  {aiModels.map((model) => (
                    <option key={model} value={model}>
                      {model}
                    </option>
                  ))}
                </select>
              </label>

              <label className="tg-row" htmlFor="api-key-input">
                <span className="tg-row-label">{copy.apiKeyLabel}</span>
                <input
                  id="api-key-input"
                  className="tg-input"
                  type="password"
                  value={apiKeyDraft}
                  onChange={(event) => {
                    setApiKeyDraft(event.target.value)
                    if (event.target.value.trim().length > 0) {
                      setRemoveStoredKeyRequested(false)
                    }
                  }}
                  placeholder={copy.apiKeyPlaceholder}
                  disabled={saving || !isOnline}
                />
              </label>
            </div>

            <label className="tg-check">
              <input
                type="checkbox"
                checked={removeStoredKeyRequested}
                onChange={(event) => {
                  setRemoveStoredKeyRequested(event.target.checked)
                  if (event.target.checked) {
                    setApiKeyDraft('')
                  }
                }}
                disabled={saving || !isOnline || !keyStatus.hasStoredKey}
              />
              <span>{copy.removeStoredKeyLabel}</span>
            </label>

            <div className="tg-inline-meta">
              <span>{copy.keySourceLabel}: {mapAiKeySource(keyStatus.source, uiLocale)}</span>
              <span>{copy.keyStoredLabel}: {keyStatus.hasStoredKey ? copy.yes : copy.no}</span>
              <span>
                {copy.modelCountLabel}: {aiModels.length}
                {modelsLoading ? ` (${copy.loadingModels})` : ''}
              </span>
            </div>
          </>
        )}
      </section>

      <section className="tg-card">
        <h3 className="tg-section-title">{copy.integrationsSection}</h3>

        <div className="kv">
          <span>{copy.oneDriveStatusLabel}</span>
          <span className={oneDriveConnected ? 'status-ok' : 'status-error'}>
            {activeGraphStatus.isConfigured
              ? (oneDriveConnected ? copy.connected : copy.disconnected)
              : copy.notConfigured}
          </span>
        </div>
        <div className="kv">
          <span>{copy.oneDriveTokenLabel}</span>
          <span>{formatDateTime(activeGraphStatus.accessTokenExpiresAtUtc, uiLocale, copy.noData)}</span>
        </div>
        <div className="kv">
          <span>{copy.notionLabel}</span>
          <span className={notionEnabled ? 'status-ok' : 'status-warn'}>
            {notionEnabled ? copy.enabled : copy.disabled}
          </span>
        </div>

        {!integrationStatus && <p className="tg-helper">{copy.loadingIntegrations}</p>}

        {loginChallenge && (
          <div className="challenge-block">
            <div className="challenge-grid">
              <span>{copy.loginCodeLabel}</span>
              <strong className="mono">{loginChallenge.userCode}</strong>
            </div>
            <a href={loginChallenge.verificationUri} target="_blank" rel="noreferrer">
              {copy.openLoginPage}
            </a>
          </div>
        )}

        <div className="actions-row">
          <button type="button" className="btn-secondary" onClick={handleRefreshStatus} disabled={saving || !isOnline}>
            {copy.refreshStatus}
          </button>
          {!oneDriveConnected && (
            <>
              <button type="button" className="btn-secondary" onClick={handleStartOneDriveLogin} disabled={saving || !isOnline}>
                {copy.startLogin}
              </button>
              <button
                type="button"
                className="btn-secondary"
                onClick={handleCompleteOneDriveLogin}
                disabled={saving || !isOnline || !loginChallenge}
              >
                {copy.finishLogin}
              </button>
            </>
          )}
          {oneDriveConnected && (
            <button type="button" className="btn-danger" onClick={handleLogoutOneDrive} disabled={saving || !isOnline}>
              {copy.logout}
            </button>
          )}
        </div>

        <details className="details-block">
          <summary>{copy.serviceActions}</summary>
          <div className="details-content">
            <div className="actions-row">
              <button type="button" className="btn-secondary" onClick={handleSyncNow} disabled={saving || !isOnline}>
                {copy.syncNow}
              </button>
              <button type="button" className="btn-secondary" onClick={handleRebuildIndex} disabled={saving || !isOnline}>
                {copy.rebuildIndex}
              </button>
              <button type="button" className="btn-danger" onClick={handleClearCache} disabled={saving || !isOnline}>
                {copy.clearCache}
              </button>
            </div>
          </div>
        </details>
      </section>

      <div className="settings-savebar">
        <div className="settings-savebar__meta">
          <span className={`save-indicator ${hasUnsavedChanges ? 'save-indicator--dirty' : 'save-indicator--clean'}`} />
          <span>{hasUnsavedChanges ? copy.unsavedChanges : copy.allSaved}</span>
        </div>
        <button
          type="button"
          className="btn-primary"
          onClick={handleSaveAll}
          disabled={saving || loading || !isOnline || !hasUnsavedChanges}
        >
          {saving ? copy.saving : copy.saveChanges}
        </button>
      </div>
    </div>
  )
}
