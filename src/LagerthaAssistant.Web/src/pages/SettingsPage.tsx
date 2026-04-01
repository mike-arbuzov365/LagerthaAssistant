import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  completeGraphLogin,
  getIntegrationNotionStatus,
  getAiKeyStatus,
  getAiModel,
  getAiProvider,
  getGraphStatus,
  graphLogout,
  graphClearCache,
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
import { formatAiKeySource, getScopedUserId } from '../lib/settings-utils'
import { buildSettingsSchema } from '../settings/settings-schema'
import { useAppStore } from '../state/appStore'

type BannerTone = 'ok' | 'error' | 'warn'

function mapSaveMode(mode: string): string {
  switch (mode) {
    case 'auto':
      return 'Автоматично'
    case 'ask':
      return 'Запитувати перед збереженням'
    case 'off':
      return 'Вимкнено'
    default:
      return mode
  }
}

function mapStorageMode(mode: string): string {
  switch (mode) {
    case 'graph':
      return 'Graph (OneDrive)'
    case 'local':
      return 'Локальне'
    default:
      return mode
  }
}

function mapPolicy(policy: string): string {
  switch (policy) {
    case 'graph_only_v1':
      return 'Лише Graph у Wave 1'
    default:
      return policy
  }
}

function mapLocale(locale: AppLocale): string {
  return locale === 'uk' ? 'Українська' : 'English'
}

function formatDateTime(value: string | null): string {
  if (!value) {
    return 'Немає'
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return new Intl.DateTimeFormat('uk-UA', {
    dateStyle: 'short',
    timeStyle: 'short',
    hour12: false,
  }).format(date)
}

function resolveBannerTone(value: string): BannerTone {
  if (value.startsWith('Помилка')) {
    return 'error'
  }

  if (value.startsWith('Увага')) {
    return 'warn'
  }

  return 'ok'
}

function StatusChip({ tone, children }: { tone: BannerTone; children: string }) {
  return <span className={`chip chip--${tone}`}>{children}</span>
}

function SectionTitle({
  icon,
  title,
  description,
}: {
  icon: string
  title: string
  description: string
}) {
  return (
    <div className="section-head">
      <span className="section-icon" aria-hidden="true">{icon}</span>
      <div>
        <h3 className="section-title">{title}</h3>
        <p className="section-description">{description}</p>
      </div>
    </div>
  )
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

  const [aiProvider, setAiProviderState] = useState('openai')
  const [aiProviders, setAiProviders] = useState<string[]>([])
  const [aiModel, setAiModelState] = useState('')
  const [aiModels, setAiModels] = useState<string[]>([])
  const [apiKey, setApiKey] = useState('')
  const [integrationStatus, setIntegrationStatus] = useState<Awaited<ReturnType<typeof getIntegrationNotionStatus>> | null>(null)
  const [graphStatus, setGraphStatus] = useState(bootstrap?.graph ?? null)
  const [loginChallenge, setLoginChallenge] = useState<GraphDeviceLoginChallengeResponse | null>(null)
  const [keyStatus, setKeyStatus] = useState<{ hasStoredKey: boolean; source: string }>({
    hasStoredKey: false,
    source: 'missing',
  })

  const settingsSchema = useMemo(
    () => buildSettingsSchema({ modules: ['core', 'lagertha'] }),
    [],
  )

  const schemaById = useMemo(
    () => new Map(settingsSchema.map((section) => [section.id, section])),
    [settingsSchema],
  )

  const titleLocale = schemaById.get('locale')
  const titleSession = schemaById.get('session')
  const titleAi = schemaById.get('ai')
  const titleOneDrive = schemaById.get('onedriveAuth')
  const titleIntegrations = schemaById.get('integrations')

  useEffect(() => {
    setLocaleDraft(locale)
  }, [locale])

  useEffect(() => {
    if (!bootstrap) {
      return
    }

    setSaveModeDraft(bootstrap.preferences.saveMode)
    setStorageModeDraft(bootstrap.preferences.storageMode)
    setGraphStatus(bootstrap.graph)
  }, [bootstrap])

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

  const loadSettings = useCallback(async () => {
    if (!bootstrap) {
      return
    }

    setLoading(true)
    setLoadError(null)

    try {
      const providerResponse = await getAiProvider(scopedUserId)
      const [modelResponse, keyResponse, notionHubStatus, graphStatusResponse] = await Promise.all([
        getAiModel(scopedUserId, providerResponse.provider),
        getAiKeyStatus(scopedUserId, providerResponse.provider),
        getIntegrationNotionStatus(),
        getGraphStatus(),
      ])

      setAiProviderState(providerResponse.provider)
      setAiProviders(providerResponse.availableProviders)
      setAiModelState(modelResponse.model)
      setAiModels(modelResponse.availableModels)
      setKeyStatus({
        hasStoredKey: keyResponse.hasStoredKey,
        source: keyResponse.apiKeySource,
      })
      setIntegrationStatus(notionHubStatus)
      setGraphStatus(graphStatusResponse)
    } catch (e) {
      setLoadError(e instanceof Error ? e.message : 'Не вдалося завантажити налаштування')
    } finally {
      setLoading(false)
    }
  }, [bootstrap, scopedUserId])

  useEffect(() => {
    if (!bootstrap) {
      return
    }

    void loadSettings()
  }, [bootstrap, loadSettings])

  if (!bootstrap || !policy) {
    return <div className="card">Немає даних bootstrap.</div>
  }

  const bootstrapData = bootstrap
  const policyData = policy
  const activeGraphStatus = graphStatus ?? bootstrapData.graph
  const policyStorageMode = policyData.allowedStorageModes[0] ?? 'graph'
  const storagePolicyMismatch = storageModeDraft !== policyStorageMode
  const storageLocked = policyData.allowedStorageModes.length === 1
  const oneDriveConnected = activeGraphStatus.isAuthenticated
  const notionEnabled = integrationStatus?.notionVocabulary.enabled || integrationStatus?.notionFood.enabled

  async function runSave(task: () => Promise<void>, successMessage: string) {
    if (!isOnline) {
      setSaveStatus('Увага: Немає мережі. Спробуйте ще раз після відновлення зʼєднання.')
      return
    }

    setSaving(true)
    setSaveStatus(null)

    try {
      await task()
      setSaveStatus(successMessage)
    } catch (e) {
      const errorMessage = e instanceof Error ? e.message : 'Операція не виконана'
      setSaveStatus(`Помилка: ${errorMessage}`)
    } finally {
      setSaving(false)
    }
  }

  async function handleSaveLocale() {
    await runSave(async () => {
      const response = await setLocale({
        locale: localeDraft,
        selectedManually: true,
        channel: 'telegram',
        userId: scopedUserId ?? undefined,
      })

      const normalized = response.locale === 'en' ? 'en' : 'uk'
      setLocaleInStore(normalized)
    }, 'Мову інтерфейсу оновлено.')
  }

  async function handleSaveSession() {
    await runSave(async () => {
      const response = await setPreferenceSession({
        saveMode: saveModeDraft,
        channel: 'telegram',
        userId: scopedUserId ?? undefined,
      })

      setSaveModeDraft(response.saveMode)
      setStorageModeDraft(response.storageMode)
      setBootstrapPreferences(response)
    }, 'Налаштування сесії оновлено.')
  }

  async function handleApplyStoragePolicy() {
    await runSave(async () => {
      const response = await setPreferenceSession({
        storageMode: policyStorageMode,
        channel: 'telegram',
        userId: scopedUserId ?? undefined,
      })

      setSaveModeDraft(response.saveMode)
      setStorageModeDraft(response.storageMode)
      setBootstrapPreferences(response)
    }, 'Політику сховища застосовано.')
  }

  async function handleSaveProvider() {
    await runSave(async () => {
      const providerResponse = await setAiProvider({
        provider: aiProvider,
        channel: 'telegram',
        userId: scopedUserId ?? undefined,
      })

      const [modelResponse, keyResponse] = await Promise.all([
        getAiModel(scopedUserId, providerResponse.provider),
        getAiKeyStatus(scopedUserId, providerResponse.provider),
      ])

      setAiProviderState(providerResponse.provider)
      setAiProviders(providerResponse.availableProviders)
      setAiModelState(modelResponse.model)
      setAiModels(modelResponse.availableModels)
      setKeyStatus({
        hasStoredKey: keyResponse.hasStoredKey,
        source: keyResponse.apiKeySource,
      })
    }, 'Провайдера AI оновлено.')
  }

  async function handleSaveModel() {
    await runSave(async () => {
      const response = await setAiModel({
        provider: aiProvider,
        model: aiModel,
        channel: 'telegram',
        userId: scopedUserId ?? undefined,
      })

      setAiModelState(response.model)
      setAiModels(response.availableModels)
    }, 'Модель AI оновлено.')
  }

  async function handleSaveApiKey() {
    if (!apiKey.trim()) {
      setSaveStatus('Помилка: Введіть API ключ перед збереженням.')
      return
    }

    await runSave(async () => {
      const response = await setAiKey({
        provider: aiProvider,
        apiKey: apiKey.trim(),
        channel: 'telegram',
        userId: scopedUserId ?? undefined,
      })

      setKeyStatus({
        hasStoredKey: response.hasStoredKey,
        source: response.apiKeySource,
      })
      setApiKey('')
    }, 'API ключ збережено.')
  }

  async function handleRemoveApiKey() {
    await runSave(async () => {
      const response = await removeAiKey(scopedUserId, aiProvider)
      setKeyStatus({
        hasStoredKey: response.hasStoredKey,
        source: response.apiKeySource,
      })
    }, 'API ключ видалено.')
  }

  async function handleSyncNow() {
    await runSave(async () => {
      await graphSyncNow()
    }, 'Синхронізацію з OneDrive виконано.')
  }

  async function handleRebuildIndex() {
    await runSave(async () => {
      await graphRebuildIndex()
    }, 'Індекс OneDrive перебудовано.')
  }

  async function handleClearCache() {
    await runSave(async () => {
      await graphClearCache()
    }, 'Кеш OneDrive очищено.')
  }

  async function handleRefreshOneDriveStatus() {
    await runSave(async () => {
      const status = await getGraphStatus()
      setGraphStatus(status)
    }, 'Статус OneDrive оновлено.')
  }

  async function handleStartOneDriveLogin() {
    await runSave(async () => {
      const response = await startGraphLogin()
      if (!response.succeeded) {
        throw new Error(response.message)
      }
      setLoginChallenge(response.challenge)
    }, 'Крок входу в OneDrive ініційовано.')
  }

  async function handleCompleteOneDriveLogin() {
    if (!loginChallenge) {
      setSaveStatus('Помилка: Спочатку ініціюйте вхід у OneDrive.')
      return
    }

    await runSave(async () => {
      const response = await completeGraphLogin(loginChallenge)
      setGraphStatus(response.status)
      if (!response.succeeded) {
        throw new Error(response.message)
      }
      setLoginChallenge(null)
    }, 'Вхід у OneDrive завершено.')
  }

  async function handleLogoutOneDrive() {
    await runSave(async () => {
      const status = await graphLogout()
      setGraphStatus(status)
      setLoginChallenge(null)
    }, 'OneDrive відключено.')
  }

  return (
    <div className="settings-page" aria-busy={saving || loading}>
      <section className="card settings-hero">
        <div className="settings-hero__header">
          <div>
            <h2 className="settings-hero__title">Центр налаштувань</h2>
            <p className="settings-hero__subtitle">
              Ті самі ключові налаштування, що у Telegram-кнопках, але в зручному Mini App форматі.
            </p>
          </div>
          <StatusChip tone={isOnline ? 'ok' : 'warn'}>{isOnline ? 'Онлайн' : 'Офлайн'}</StatusChip>
        </div>

        <div className="settings-overview">
          <div className="overview-item">
            <span className="overview-label">Мова</span>
            <strong className="overview-value">{mapLocale(locale)}</strong>
          </div>
          <div className="overview-item">
            <span className="overview-label">Режим збереження</span>
            <strong className="overview-value">{mapSaveMode(saveModeDraft)}</strong>
          </div>
          <div className="overview-item">
            <span className="overview-label">AI</span>
            <strong className="overview-value">{aiProvider} / {aiModel || '—'}</strong>
          </div>
          <div className="overview-item">
            <span className="overview-label">OneDrive / Graph</span>
            <strong className={`overview-value ${oneDriveConnected ? 'status-ok' : 'status-error'}`}>
              {oneDriveConnected ? 'Підключено' : 'Не підключено'}
            </strong>
          </div>
          <div className="overview-item">
            <span className="overview-label">Notion</span>
            <strong className={`overview-value ${notionEnabled ? 'status-ok' : 'status-warn'}`}>
              {notionEnabled ? 'Активно' : 'Не налаштовано'}
            </strong>
          </div>
        </div>
      </section>

      {saveStatus && (
        <section
          className={`status-banner status-banner--${resolveBannerTone(saveStatus)}`}
          role={saveStatus.startsWith('Помилка') ? 'alert' : 'status'}
          aria-live="polite"
        >
          {saveStatus}
        </section>
      )}

      {loadError && (
        <section className="status-banner status-banner--error" role="alert">
          Не вдалося завантажити дані налаштувань: {loadError}
          <button type="button" className="btn-secondary" onClick={() => void loadSettings()} disabled={saving}>
            Спробувати знову
          </button>
        </section>
      )}

      <section className="card settings-section" data-settings-section="locale">
        <SectionTitle
          icon="🌐"
          title={titleLocale?.title ?? 'Мова інтерфейсу'}
          description={titleLocale?.description ?? 'Основна мова Mini App. За замовчуванням — українська.'}
        />
        <div className="field">
          <label htmlFor="locale-select">Оберіть мову</label>
          <select
            id="locale-select"
            value={localeDraft}
            onChange={(e) => setLocaleDraft(e.target.value === 'en' ? 'en' : 'uk')}
            disabled={saving || !isOnline}
          >
            <option value="uk">Українська</option>
            <option value="en">English</option>
          </select>
        </div>
        <div className="actions-row">
          <button type="button" className="btn-primary" onClick={handleSaveLocale} disabled={saving || !isOnline}>
            Зберегти мову
          </button>
        </div>
      </section>

      <section className="card settings-section" data-settings-section="session">
        <SectionTitle
          icon="💾"
          title={titleSession?.title ?? 'Сесія і сховище'}
          description={titleSession?.description ?? 'Режими збереження та політика сховища для поточного каналу.'}
        />

        <div className="field-grid">
          <div className="field">
            <label htmlFor="save-mode-select">Режим збереження</label>
            <select
              id="save-mode-select"
              value={saveModeDraft}
              onChange={(e) => setSaveModeDraft(e.target.value)}
              disabled={saving || !isOnline}
            >
              {bootstrapData.preferences.availableSaveModes.map((saveMode) => (
                <option key={saveMode} value={saveMode}>
                  {mapSaveMode(saveMode)}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="storage-mode-select">Режим сховища</label>
            <select
              id="storage-mode-select"
              value={storageModeDraft}
              onChange={(e) => setStorageModeDraft(e.target.value)}
              disabled={saving || !isOnline || storageLocked}
            >
              {(storageLocked ? policyData.allowedStorageModes : bootstrapData.preferences.availableStorageModes).map((mode) => (
                <option key={mode} value={mode}>
                  {mapStorageMode(mode)}
                </option>
              ))}
            </select>
          </div>
        </div>

        <p className="helper-text">
          Політика Wave 1: <strong>{mapPolicy(policyData.storageModePolicy)}</strong>
        </p>

        <div className="actions-row">
          <button type="button" className="btn-primary" onClick={handleSaveSession} disabled={saving || !isOnline}>
            Зберегти режими
          </button>
          {storagePolicyMismatch && (
            <button type="button" className="btn-secondary" onClick={handleApplyStoragePolicy} disabled={saving || !isOnline}>
              Повернути policy-режим
            </button>
          )}
        </div>
      </section>

      <section className="card settings-section" data-settings-section="ai">
        <SectionTitle
          icon="🤖"
          title={titleAi?.title ?? 'AI налаштування'}
          description={titleAi?.description ?? 'Керування провайдером, моделлю та API ключем.'}
        />

        {loading && (
          <p className="muted-text" role="status" aria-live="polite">
            Завантаження AI налаштувань...
          </p>
        )}

        {!loading && (
          <>
            <div className="field-grid">
              <div className="field">
                <label htmlFor="provider-select">Провайдер</label>
                <select
                  id="provider-select"
                  value={aiProvider}
                  onChange={(e) => setAiProviderState(e.target.value)}
                  disabled={saving || !isOnline}
                >
                  {aiProviders.map((provider) => (
                    <option key={provider} value={provider}>
                      {provider}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label htmlFor="model-select">Модель</label>
                <select
                  id="model-select"
                  value={aiModel}
                  onChange={(e) => setAiModelState(e.target.value)}
                  disabled={saving || !isOnline}
                >
                  {aiModels.map((model) => (
                    <option key={model} value={model}>
                      {model}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="actions-row">
              <button type="button" className="btn-secondary" onClick={handleSaveProvider} disabled={saving || !isOnline}>
                Зберегти провайдера
              </button>
              <button type="button" className="btn-secondary" onClick={handleSaveModel} disabled={saving || !isOnline}>
                Зберегти модель
              </button>
            </div>

            <div className="field">
              <label htmlFor="api-key-input">API ключ</label>
              <input
                id="api-key-input"
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder="Введіть ключ для обраного провайдера"
                disabled={saving || !isOnline}
              />
            </div>

            <div className="actions-row">
              <button type="button" className="btn-primary" onClick={handleSaveApiKey} disabled={saving || !isOnline}>
                Зберегти ключ
              </button>
              <button type="button" className="btn-danger" onClick={handleRemoveApiKey} disabled={saving || !isOnline}>
                Видалити ключ
              </button>
            </div>

            <div className="kv">
              <span>Статус ключа</span>
              <span>{formatAiKeySource(keyStatus.source)}</span>
            </div>
            <div className="kv">
              <span>Ключ у сховищі</span>
              <span>{keyStatus.hasStoredKey ? 'Так' : 'Ні'}</span>
            </div>
          </>
        )}
      </section>

      <section className="card settings-section" data-settings-section="onedrive-auth">
        <SectionTitle
          icon="☁️"
          title={titleOneDrive?.title ?? 'OneDrive авторизація'}
          description={titleOneDrive?.description ?? 'Підключення та керування доступом до Graph.'}
        />

        <div className="kv">
          <span>Конфігурація</span>
          <span>{activeGraphStatus.isConfigured ? 'Готово' : 'Не налаштовано'}</span>
        </div>
        <div className="kv">
          <span>Підключення</span>
          <span className={oneDriveConnected ? 'status-ok' : 'status-error'}>
            {oneDriveConnected ? 'Активне' : 'Не активне'}
          </span>
        </div>
        <div className="kv">
          <span>Термін токена</span>
          <span>{formatDateTime(activeGraphStatus.accessTokenExpiresAtUtc)}</span>
        </div>
        <div className="kv">
          <span>Повідомлення</span>
          <span>{activeGraphStatus.message || '—'}</span>
        </div>

        {loginChallenge && (
          <div className="challenge-block">
            <div className="challenge-grid">
              <span>Код входу</span>
              <strong className="mono">{loginChallenge.userCode}</strong>
            </div>
            <div className="challenge-grid">
              <span>Сторінка підтвердження</span>
              <a href={loginChallenge.verificationUri} target="_blank" rel="noreferrer">
                Відкрити сторінку входу
              </a>
            </div>
          </div>
        )}

        <div className="actions-row">
          <button type="button" className="btn-secondary" onClick={handleRefreshOneDriveStatus} disabled={saving || !isOnline}>
            Оновити статус
          </button>
          {!oneDriveConnected && (
            <>
              <button type="button" className="btn-secondary" onClick={handleStartOneDriveLogin} disabled={saving || !isOnline}>
                Почати вхід
              </button>
              <button
                type="button"
                className="btn-primary"
                onClick={handleCompleteOneDriveLogin}
                disabled={saving || !isOnline || !loginChallenge}
              >
                Завершити вхід
              </button>
            </>
          )}
          {oneDriveConnected && (
            <button type="button" className="btn-danger" onClick={handleLogoutOneDrive} disabled={saving || !isOnline}>
              Вийти з OneDrive
            </button>
          )}
        </div>
      </section>

      <section className="card settings-section" data-settings-section="integrations">
        <SectionTitle
          icon="📝"
          title={titleIntegrations?.title ?? 'Інтеграції (Notion)'}
          description={titleIntegrations?.description ?? 'Операційний стан синхронізацій Vocabulary та Food.'}
        />

        {!integrationStatus && (
          <p className="muted-text" role="status" aria-live="polite">
            Завантаження статусів інтеграцій...
          </p>
        )}

        {integrationStatus && (
          <>
            <div className="kv">
              <span>Notion Vocabulary</span>
              <span className={integrationStatus.notionVocabulary.enabled ? 'status-ok' : 'status-warn'}>
                {integrationStatus.notionVocabulary.enabled ? 'Увімкнено' : 'Вимкнено'}
              </span>
            </div>
            <div className="kv">
              <span>Черга / помилки</span>
              <span>{integrationStatus.notionVocabulary.pendingCards} / {integrationStatus.notionVocabulary.failedCards}</span>
            </div>
            <div className="kv">
              <span>Notion Food</span>
              <span className={integrationStatus.notionFood.enabled ? 'status-ok' : 'status-warn'}>
                {integrationStatus.notionFood.enabled ? 'Увімкнено' : 'Вимкнено'}
              </span>
            </div>
            <div className="kv">
              <span>Inventory + Grocery (pending/failed)</span>
              <span>
                {integrationStatus.notionFood.inventoryPendingOrFailed + integrationStatus.notionFood.groceryPendingOrFailed}
              </span>
            </div>
          </>
        )}

        <div className="actions-row">
          <button type="button" className="btn-secondary" onClick={() => void loadSettings()} disabled={saving || loading}>
            Оновити інтеграції
          </button>
        </div>
      </section>

      <details className="details-block">
        <summary>Розширені та технічні дані</summary>

        <div className="details-content">
          <section className="card settings-subcard" data-settings-section="state">
            <h4>Діагностика сесії</h4>
            <div className="kv">
              <span>Канал</span>
              <span>{bootstrapData.scope.channel}</span>
            </div>
            <div className="kv">
              <span>Користувач</span>
              <span>{bootstrapData.scope.userId}</span>
            </div>
            <div className="kv">
              <span>Режим збереження</span>
              <span>{mapSaveMode(bootstrapData.preferences.saveMode)}</span>
            </div>
            <div className="kv">
              <span>Режим сховища</span>
              <span>{mapStorageMode(bootstrapData.preferences.storageMode)}</span>
            </div>
          </section>

          <section className="card settings-subcard" data-settings-section="policy">
            <h4>Policy Mini App v1</h4>
            <div className="kv">
              <span>Мова за замовчуванням</span>
              <span>{policyData.defaultLocale}</span>
            </div>
            <div className="kv">
              <span>Політика сховища</span>
              <span>{mapPolicy(policyData.storageModePolicy)}</span>
            </div>
            <div className="kv">
              <span>Дозволені режими</span>
              <span>{policyData.allowedStorageModes.map(mapStorageMode).join(', ')}</span>
            </div>
            <div className="kv">
              <span>Перевірка initData</span>
              <span>{policyData.requiresInitDataVerification ? 'Обовʼязкова' : 'Вимкнена'}</span>
            </div>
            <ul className="notes-list">
              {policyData.notes.map((note) => (
                <li key={note}>{note}</li>
              ))}
            </ul>
          </section>

          <section className="card settings-subcard" data-settings-section="onedrive-ops">
            <h4>Сервісні операції OneDrive</h4>
            <p className="helper-text">Використовуйте ці дії лише за потреби технічного обслуговування.</p>
            <div className="actions-row">
              <button type="button" className="btn-secondary" onClick={handleSyncNow} disabled={saving || !isOnline}>
                Синхронізувати зараз
              </button>
              <button type="button" className="btn-secondary" onClick={handleRebuildIndex} disabled={saving || !isOnline}>
                Перебудувати індекс
              </button>
              <button type="button" className="btn-danger" onClick={handleClearCache} disabled={saving || !isOnline}>
                Очистити кеш
              </button>
            </div>
          </section>
        </div>
      </details>
    </div>
  )
}
