import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import {
  commitMiniAppSettings,
  completeGraphLogin,
  getAiKeyStatus,
  getAiModel,
  getGraphStatus,
  getIntegrationNotionStatus,
  graphClearCache,
  graphLogout,
  graphRebuildIndex,
  graphSyncNow,
  startGraphLogin,
} from '../api/client'
import type { ReactNode } from 'react'
import type { GraphDeviceLoginChallengeResponse, MiniAppSettingsCommitResponse } from '../api/contracts'
import { emitMiniAppDiagnostic } from '../lib/miniAppDiagnostics'
import type { AppLocale } from '../lib/locale'
import { getScopedUserId } from '../lib/settings-utils'
import { normalizeThemeMode, resolveAppliedTheme, type AppThemeMode } from '../lib/theme'
import {
  applyTelegramClosingConfirmation,
  buildUnsavedChangesPrompt,
  closeTelegramMiniApp,
  hasUnsavedSettingsChanges,
  normalizeLocaleFromPreference,
  type PersistedSnapshot,
  resolveTelegramMiniAppBridge,
  syncTelegramClosingConfirmation,
  type TelegramClosingConfirmationWebApp,
  waitForTelegramMiniAppBridge,
} from './settings-page-utils'
import {
  buildLanguageChoices,
  buildSaveModeChoices,
  buildStorageModeChoices,
  buildThemeChoices,
  formatModelLabel,
  formatProviderLabel,
  mapAiKeySource,
  mapStorageMode,
  resolveNotionVisual,
  resolveOneDriveVisual,
  type IntegrationTone,
  type SettingChoiceOption,
} from './settings-page-presenter'
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
  generalIntro: string
  aiIntro: string
  integrationsIntro: string
  languageLabel: string
  themeLabel: string
  saveModeLabel: string
  storageModeLabel: string
  providerLabel: string
  modelLabel: string
  apiKeyLabel: string
  apiKeyPlaceholder: string
  languageHint: string
  themeHint: string
  saveModeHint: string
  storageModeHint: string
  providerHint: string
  modelHint: string
  apiKeyHint: string
  removeStoredKeyLabel: string
  keySourceLabel: string
  keyStoredLabel: string
  modelCountLabel: string
  oneDriveStatusLabel: string
  oneDriveTokenLabel: string
  notionLabel: string
  notionVocabularyLabel: string
  notionFoodLabel: string
  oneDriveSubtitle: string
  notionSubtitle: string
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
  serviceActionsHint: string
  syncNow: string
  rebuildIndex: string
  clearCache: string
  loginCodeLabel: string
  openLoginPage: string
  enterCodeFirst: string
  storageLockedHint: string
  refreshHint: string
  startLoginHint: string
  finishLoginHint: string
  logoutHint: string
  syncNowHint: string
  rebuildIndexHint: string
  clearCacheHint: string
}

const copyByLocale: Record<AppLocale, CopyPack> = {
  uk: {
    screenTitle: 'Налаштування Lagertha',
    screenSubtitle: 'Єдиний екран для мови, AI та інтеграцій.',
    online: 'Онлайн',
    offline: 'Офлайн',
    noBootstrap: 'Немає bootstrap-даних.',
    loadingAi: 'Завантаження AI-налаштувань…',
    loadingIntegrations: 'Завантаження статусів інтеграцій…',
    loadingModels: 'Оновлюємо список моделей…',
    retry: 'Спробувати знову',
    generalSection: 'Загальні',
    aiSection: 'AI',
    integrationsSection: 'Інтеграції',
    generalIntro: 'Базові правила інтерфейсу та збереження для поточної сесії.',
    aiIntro: 'Провайдер, модель та секрети доступу для AI-частини Lagertha.',
    integrationsIntro: 'Статуси підключень і сервісні дії без втрати Telegram-функціоналу.',
    languageLabel: 'Мова інтерфейсу',
    themeLabel: 'Тема',
    saveModeLabel: 'Режим збереження',
    storageModeLabel: 'Режим сховища',
    providerLabel: 'Провайдер',
    modelLabel: 'Модель',
    apiKeyLabel: 'API ключ',
    apiKeyPlaceholder: 'Введіть новий ключ (необов’язково)',
    languageHint: 'Виберіть мову, якою Lagertha покаже Mini App та основні Telegram-меню.',
    themeHint: 'Оберіть, чи мають налаштування наслідувати тему Telegram, чи бути завжди світлими або темними.',
    saveModeHint: 'Визначає, коли бот записує дані у сховище.',
    storageModeHint: 'Де зберігається робочий контекст поточної хвилі налаштувань.',
    providerHint: 'Основний AI-провайдер для відповідей і допоміжних сценаріїв.',
    modelHint: 'Список моделей підлаштовується під вибраного провайдера.',
    apiKeyHint: 'Критичне поле. Новий ключ застосовується лише після збереження.',
    removeStoredKeyLabel: 'Видалити збережений ключ при збереженні',
    keySourceLabel: 'Джерело ключа',
    keyStoredLabel: 'Ключ у сховищі',
    modelCountLabel: 'Доступно моделей',
    oneDriveStatusLabel: 'OneDrive / Graph',
    oneDriveTokenLabel: 'Токен до',
    notionLabel: 'Notion',
    notionVocabularyLabel: 'Словник',
    notionFoodLabel: 'Food',
    oneDriveSubtitle: 'Синхронізація файлів, черг та індексу знань.',
    notionSubtitle: 'Майбутній центр інтеграцій для кількох просторів і ботів.',
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
    serviceActionsHint: 'Обережні операції для синхронізації, індексу та кешу.',
    syncNow: 'Синхронізувати зараз',
    rebuildIndex: 'Перебудувати індекс',
    clearCache: 'Очистити кеш',
    loginCodeLabel: 'Код входу',
    openLoginPage: 'Відкрити сторінку входу',
    enterCodeFirst: 'Спочатку ініціюйте вхід у OneDrive.',
    storageLockedHint: 'Режим сховища обмежено policy Wave 1.',
    refreshHint: 'Перечитати актуальний стан інтеграцій.',
    startLoginHint: 'Запустити device-code flow для OneDrive.',
    finishLoginHint: 'Завершити вхід після підтвердження в браузері.',
    logoutHint: 'Відв’язати OneDrive від поточної сесії.',
    syncNowHint: 'Форсувати синхронізацію без очікування воркера.',
    rebuildIndexHint: 'Повністю перебудувати індекс OneDrive-даних.',
    clearCacheHint: 'Скинути локальний кеш сервісу й перечитати стан заново.',
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
    generalIntro: 'Base interface and persistence rules for the current session.',
    aiIntro: 'Provider, model, and secret management for the AI layer of Lagertha.',
    integrationsIntro: 'Connection health and service actions without losing Telegram functionality.',
    languageLabel: 'Interface language',
    themeLabel: 'Theme',
    saveModeLabel: 'Save mode',
    storageModeLabel: 'Storage mode',
    providerLabel: 'Provider',
    modelLabel: 'Model',
    apiKeyLabel: 'API key',
    apiKeyPlaceholder: 'Enter a new key (optional)',
    languageHint: 'Choose the language used by the Mini App and the main Telegram menus.',
    themeHint: 'Choose whether settings should follow Telegram, stay light, or stay dark.',
    saveModeHint: 'Defines when the bot writes data into persistent storage.',
    storageModeHint: 'Controls where the current Wave 1 context is stored.',
    providerHint: 'Primary AI provider used by assistant flows and completions.',
    modelHint: 'The model list updates automatically for the selected provider.',
    apiKeyHint: 'Critical field. A new key is applied only after saving.',
    removeStoredKeyLabel: 'Remove stored key on save',
    keySourceLabel: 'Key source',
    keyStoredLabel: 'Key in storage',
    modelCountLabel: 'Models available',
    oneDriveStatusLabel: 'OneDrive / Graph',
    oneDriveTokenLabel: 'Token valid until',
    notionLabel: 'Notion',
    notionVocabularyLabel: 'Vocabulary',
    notionFoodLabel: 'Food',
    oneDriveSubtitle: 'File sync, queue processing, and knowledge index maintenance.',
    notionSubtitle: 'Future-ready integration hub for multiple workspaces and bots.',
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
    serviceActionsHint: 'Careful operations for sync, index, and cache maintenance.',
    syncNow: 'Sync now',
    rebuildIndex: 'Rebuild index',
    clearCache: 'Clear cache',
    loginCodeLabel: 'Login code',
    openLoginPage: 'Open login page',
    enterCodeFirst: 'Start OneDrive login first.',
    storageLockedHint: 'Storage mode is locked by Wave 1 policy.',
    refreshHint: 'Reload the latest integration health and status.',
    startLoginHint: 'Start the OneDrive device-code authorization flow.',
    finishLoginHint: 'Complete sign-in after confirming it in the browser.',
    logoutHint: 'Disconnect OneDrive from the current session.',
    syncNowHint: 'Force sync immediately instead of waiting for the worker.',
    rebuildIndexHint: 'Fully rebuild the OneDrive data index.',
    clearCacheHint: 'Reset the local service cache and fetch fresh state again.',
  },
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

function GlyphSvg({
  children,
  className = 'choice-card__glyph-svg',
}: {
  children: ReactNode
  className?: string
}) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {children}
    </svg>
  )
}

function ChoiceIcon({ icon }: { icon: string }) {
  if (icon === 'flag-uk') {
    return (
      <span className="choice-card__flag" aria-hidden="true">
        <span className="choice-card__flag-band choice-card__flag-band--blue" />
        <span className="choice-card__flag-band choice-card__flag-band--yellow" />
      </span>
    )
  }

  if (icon === 'flag-gb') {
    return (
      <span className="choice-card__flag choice-card__flag--gb" aria-hidden="true">
        <span className="choice-card__flag-gb-diagonal choice-card__flag-gb-diagonal--a" />
        <span className="choice-card__flag-gb-diagonal choice-card__flag-gb-diagonal--b" />
        <span className="choice-card__flag-gb-cross choice-card__flag-gb-cross--h" />
        <span className="choice-card__flag-gb-cross choice-card__flag-gb-cross--v" />
      </span>
    )
  }

  if (icon === 'theme-system') {
    return (
      <GlyphSvg>
        <rect x="4.5" y="5" width="15" height="10.5" rx="2.2" />
        <path d="M9.5 19h5" />
        <path d="M12 15.5V19" />
      </GlyphSvg>
    )
  }

  if (icon === 'theme-light') {
    return (
      <GlyphSvg>
        <circle cx="12" cy="12" r="3.6" />
        <path d="M12 3.5v2.3" />
        <path d="M12 18.2v2.3" />
        <path d="M4 12h2.3" />
        <path d="M17.7 12H20" />
        <path d="m6.1 6.1 1.6 1.6" />
        <path d="m16.3 16.3 1.6 1.6" />
        <path d="m16.3 7.7 1.6-1.6" />
        <path d="m6.1 17.9 1.6-1.6" />
      </GlyphSvg>
    )
  }

  if (icon === 'theme-dark') {
    return (
      <GlyphSvg className="choice-card__glyph-svg choice-card__glyph-svg--filled">
        <path
          d="M15.5 4.8c-1.2.2-2.4.9-3.2 1.9a6.5 6.5 0 0 0 5 10.7 6.5 6.5 0 0 1-8.8-8.9 6.5 6.5 0 0 1 7-3.7Z"
          fill="currentColor"
          stroke="none"
        />
      </GlyphSvg>
    )
  }

  if (icon === 'hero-settings') {
    return (
      <GlyphSvg>
        <circle cx="12" cy="12" r="3.2" />
        <path d="M12 2.8v2.1" />
        <path d="M12 19.1v2.1" />
        <path d="m5.5 5.5 1.5 1.5" />
        <path d="m17 17 1.5 1.5" />
        <path d="M2.8 12h2.1" />
        <path d="M19.1 12h2.1" />
        <path d="m5.5 18.5 1.5-1.5" />
        <path d="M17 7l1.5-1.5" />
      </GlyphSvg>
    )
  }

  if (icon === 'service-cloud') {
    return (
      <GlyphSvg>
        <path d="M7.5 17a3.5 3.5 0 1 1 .7-6.9A5 5 0 0 1 18 11.3a3 3 0 0 1-.5 5.7Z" />
      </GlyphSvg>
    )
  }

  return <span className="choice-card__glyph" aria-hidden="true">{icon}</span>
}

interface ChoiceGridProps {
  options: SettingChoiceOption[]
  value: string
  onChange: (value: string) => void
  disabled?: boolean
  columns?: 'compact' | 'stack'
}

function ChoiceGrid({
  options,
  value,
  onChange,
  disabled = false,
  columns = 'stack',
}: ChoiceGridProps) {
  return (
    <div className={`choice-grid choice-grid--${columns}`} role="list">
      {options.map((option) => {
        const selected = option.value === value

        return (
          <button
            key={option.value}
            type="button"
            className={`choice-card ${selected ? 'choice-card--selected' : ''} ${option.icon ? 'choice-card--icon' : ''} ${option.description ? '' : 'choice-card--compact'}`.trim()}
            onClick={() => onChange(option.value)}
            disabled={disabled}
            aria-pressed={selected}
            data-choice-value={option.value}
            data-choice-icon={option.icon ?? ''}
          >
            <span className="choice-card__main">
              {option.icon ? <span className="choice-card__icon" aria-hidden="true"><ChoiceIcon icon={option.icon} /></span> : null}
              <span className="choice-card__title">{option.title}</span>
            </span>
            {option.description ? <span className="choice-card__description">{option.description}</span> : null}
          </button>
        )
      })}
    </div>
  )
}

interface ChoiceChipOption {
  value: string
  label: string
}

interface ChoiceChipGroupProps {
  options: ChoiceChipOption[]
  value: string
  onChange: (value: string) => void
  disabled?: boolean
}

function ChoiceChipGroup({
  options,
  value,
  onChange,
  disabled = false,
}: ChoiceChipGroupProps) {
  return (
    <div className="choice-chip-group" role="list">
      {options.map((option) => {
        const selected = option.value === value

        return (
          <button
            key={option.value}
            type="button"
            className={`choice-chip ${selected ? 'choice-chip--selected' : ''}`}
            onClick={() => onChange(option.value)}
            disabled={disabled}
            aria-pressed={selected}
          >
            {option.label}
          </button>
        )
      })}
    </div>
  )
}

interface ActionTileProps {
  title: string
  hint: string
  tone?: 'secondary' | 'danger'
  onClick: () => void
  disabled?: boolean
}

function ActionTile({
  title,
  hint,
  tone = 'secondary',
  onClick,
  disabled = false,
}: ActionTileProps) {
  return (
    <button
      type="button"
      className={`action-tile action-tile--${tone}`}
      onClick={onClick}
      disabled={disabled}
    >
      <span className="action-tile__title">{title}</span>
      <span className="action-tile__hint">{hint}</span>
    </button>
  )
}

interface IntegrationFactProps {
  label: string
  value: string
}

function IntegrationFact({ label, value }: IntegrationFactProps) {
  return (
    <div className="integration-fact">
      <span className="integration-fact__label">{label}</span>
      <strong className="integration-fact__value">{value}</strong>
    </div>
  )
}

interface IntegrationCardProps {
  icon: ReactNode
  title: string
  subtitle: string
  tone: IntegrationTone
  status: string
  facts: IntegrationFactProps[]
  actions?: ReactNode
  children?: ReactNode
}

function IntegrationCard({
  icon,
  title,
  subtitle,
  tone,
  status,
  facts,
  actions,
  children,
}: IntegrationCardProps) {
  return (
    <article className="integration-card">
      <div className="integration-card__header">
        <div className="integration-card__icon" aria-hidden="true">{icon}</div>
        <div className="integration-card__title-wrap">
          <div className="integration-card__title-row">
            <h4 className="integration-card__title">{title}</h4>
            <StatusChip tone={tone}>{status}</StatusChip>
          </div>
          <p className="integration-card__subtitle">{subtitle}</p>
        </div>
      </div>

      <div className="integration-facts">
        {facts.map((fact) => (
          <IntegrationFact key={`${fact.label}-${fact.value}`} label={fact.label} value={fact.value} />
        ))}
      </div>

      {children}
      {actions ? <div className="integration-actions">{actions}</div> : null}
    </article>
  )
}

export function SettingsPage() {
  const locale = useAppStore((s) => s.locale)
  const persistedThemeMode = useAppStore((s) => s.themeMode)
  const bootstrap = useAppStore((s) => s.bootstrap)
  const host = useAppStore((s) => s.host)
  const policy = useAppStore((s) => s.policy)
  const setLocaleInStore = useAppStore((s) => s.setLocale)
  const setThemeModeInStore = useAppStore((s) => s.setThemeMode)
  const setBootstrapPreferences = useAppStore((s) => s.setBootstrapPreferences)
  const setBootstrapSettings = useAppStore((s) => s.setBootstrapSettings)

  const scopedUserId = useMemo(
    () => getScopedUserId(bootstrap?.scope.userId),
    [bootstrap?.scope.userId],
  )

  const loading = false
  const [loadError, setLoadError] = useState<string | null>(null)
  const [saveStatus, setSaveStatus] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [isOnline, setIsOnline] = useState(() => (typeof navigator === 'undefined' ? true : navigator.onLine))

  const [localeDraft, setLocaleDraft] = useState<AppLocale>(locale)
  const [saveModeDraft, setSaveModeDraft] = useState(bootstrap?.preferences.saveMode ?? 'ask')
  const [storageModeDraft, setStorageModeDraft] = useState(bootstrap?.preferences.storageMode ?? 'graph')
  const [themeModeDraft, setThemeModeDraft] = useState<AppThemeMode>(normalizeThemeMode(bootstrap?.settings.themeMode))

  const [aiProviderDraft, setAiProviderDraft] = useState(bootstrap?.settings.aiProvider ?? 'openai')
  const [aiProviders, setAiProviders] = useState<string[]>(bootstrap?.settings.availableProviders ?? [])
  const [aiModelDraft, setAiModelDraft] = useState(bootstrap?.settings.aiModel ?? '')
  const [aiModels, setAiModels] = useState<string[]>(bootstrap?.settings.availableModels ?? [])
  const [modelsLoading, setModelsLoading] = useState(false)

  const [apiKeyDraft, setApiKeyDraft] = useState('')
  const [removeStoredKeyRequested, setRemoveStoredKeyRequested] = useState(false)

  const [integrationStatus, setIntegrationStatus] = useState<Awaited<ReturnType<typeof getIntegrationNotionStatus>> | null>(bootstrap?.settings.notion ?? null)
  const [graphStatus, setGraphStatus] = useState(bootstrap?.graph ?? null)
  const [loginChallenge, setLoginChallenge] = useState<GraphDeviceLoginChallengeResponse | null>(null)
  const [keyStatus, setKeyStatus] = useState<{ hasStoredKey: boolean; source: string }>({
    hasStoredKey: bootstrap?.settings.hasStoredKey ?? false,
    source: bootstrap?.settings.apiKeySource ?? 'missing',
  })
  const [snapshot, setSnapshot] = useState<PersistedSnapshot | null>(() => (
    bootstrap
      ? {
        locale,
        saveMode: bootstrap.preferences.saveMode,
        storageMode: bootstrap.preferences.storageMode,
        themeMode: normalizeThemeMode(bootstrap.settings.themeMode),
        aiProvider: bootstrap.settings.aiProvider,
        aiModel: bootstrap.settings.aiModel,
      }
      : null
  ))

  const providerRequestVersion = useRef(0)
  const skipInitialProviderRefreshRef = useRef(true)
  const [bridgeReadyVersion, setBridgeReadyVersion] = useState(0)
  const missingBridgeLoggedRef = useRef(false)

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
  const scopedConversationId = bootstrap?.scope.conversationId ?? null

  useEffect(() => {
    if (!host?.isTelegram) {
      return
    }

    let cancelled = false
    let attempts = 0

    const tick = () => {
      const bridge = resolveTelegramMiniAppBridge()
      if (bridge) {
        if (!cancelled) {
          setBridgeReadyVersion((version) => version + 1)
          emitMiniAppDiagnostic({
            eventType: 'bridge.available',
            severity: 'info',
            message: 'Telegram WebApp bridge became available.',
            isTelegram: true,
            hostSource: host.source,
            platform: host.platform,
            channel: bootstrap?.scope.channel,
            userId: bootstrap?.scope.userId ?? host.userId,
            conversationId: bootstrap?.scope.conversationId ?? host.conversationId,
            hasInitData: host.initData.length > 0,
            hasWebApp: true,
            locale: localeDraft,
          })
        }
        return
      }

      attempts += 1
      if (attempts >= 16) {
        if (!missingBridgeLoggedRef.current) {
          missingBridgeLoggedRef.current = true
          emitMiniAppDiagnostic({
            eventType: 'bridge.missing',
            severity: 'warn',
            message: 'Telegram launch params detected, but WebApp bridge is still unavailable.',
            isTelegram: true,
            hostSource: host.source,
            platform: host.platform,
            channel: bootstrap?.scope.channel,
            userId: bootstrap?.scope.userId ?? host.userId,
            conversationId: bootstrap?.scope.conversationId ?? host.conversationId,
            hasInitData: host.initData.length > 0,
            hasWebApp: false,
            locale: localeDraft,
          })
        }
        return
      }

      window.setTimeout(tick, 60)
    }

    tick()

    return () => {
      cancelled = true
    }
  }, [bootstrap?.scope.channel, bootstrap?.scope.conversationId, bootstrap?.scope.userId, host, localeDraft])

  useEffect(() => {
    if (!bootstrap) {
      return
    }

    const normalizedLocale = locale
    const saveModeFromBootstrap = bootstrap.preferences.saveMode
    const storageModeFromBootstrap = bootstrap.preferences.storageMode
    const themeModeFromBootstrap = normalizeThemeMode(bootstrap.settings.themeMode)

    setLocaleDraft(normalizedLocale)
    setSaveModeDraft(saveModeFromBootstrap)
    setStorageModeDraft(storageModeFromBootstrap)
    setThemeModeDraft(themeModeFromBootstrap)
    setAiProviderDraft(bootstrap.settings.aiProvider)
    setAiProviders(bootstrap.settings.availableProviders)
    setAiModelDraft(bootstrap.settings.aiModel)
    setAiModels(bootstrap.settings.availableModels)
    setKeyStatus({
      hasStoredKey: bootstrap.settings.hasStoredKey,
      source: bootstrap.settings.apiKeySource,
    })
    setIntegrationStatus(bootstrap.settings.notion)
    setGraphStatus(bootstrap.graph)
    setSnapshot({
      locale: normalizedLocale,
      saveMode: saveModeFromBootstrap,
      storageMode: storageModeFromBootstrap,
      themeMode: themeModeFromBootstrap,
      aiProvider: bootstrap.settings.aiProvider,
      aiModel: bootstrap.settings.aiModel,
    })
    skipInitialProviderRefreshRef.current = true
  }, [bootstrap, locale])

  useEffect(() => {
    if (!bootstrap || !isOnline) {
      return
    }

    let cancelled = false
    const statusRefreshStartedAt = performance.now()

    void (async () => {
      try {
        const [notionStatus, currentGraphStatus] = await Promise.all([
          getIntegrationNotionStatus(),
          getGraphStatus(),
        ])

        if (cancelled) {
          return
        }

        setIntegrationStatus(notionStatus)
        setGraphStatus(currentGraphStatus)
        emitMiniAppDiagnostic({
          eventType: 'bootstrap.status_refresh_success',
          severity: 'info',
          message: 'Background runtime statuses refreshed after bootstrap.',
          isTelegram: host?.isTelegram,
          hostSource: host?.source,
          platform: host?.platform,
          channel: bootstrap.scope.channel,
          userId: bootstrap.scope.userId,
          conversationId: bootstrap.scope.conversationId,
          hasInitData: Boolean(host?.initData),
          hasWebApp: Boolean(resolveTelegramMiniAppBridge()),
          locale,
          details: {
            statusRefreshMs: Math.round(performance.now() - statusRefreshStartedAt),
            graphAuthenticated: currentGraphStatus.isAuthenticated,
            notionVocabularyEnabled: notionStatus.notionVocabulary.enabled,
            notionFoodEnabled: notionStatus.notionFood.enabled,
          },
        })
      } catch (error) {
        if (cancelled) {
          return
        }

        emitMiniAppDiagnostic({
          eventType: 'bootstrap.status_refresh_failure',
          severity: 'warn',
          message: error instanceof Error ? error.message : 'Status refresh failed.',
          isTelegram: host?.isTelegram,
          hostSource: host?.source,
          platform: host?.platform,
          channel: bootstrap.scope.channel,
          userId: bootstrap.scope.userId,
          conversationId: bootstrap.scope.conversationId,
          hasInitData: Boolean(host?.initData),
          hasWebApp: Boolean(resolveTelegramMiniAppBridge()),
          locale,
          details: {
            statusRefreshMs: Math.round(performance.now() - statusRefreshStartedAt),
          },
        })
      }
    })()

    return () => {
      cancelled = true
    }
  }, [
    bootstrap?.scope.channel,
    bootstrap?.scope.conversationId,
    bootstrap?.scope.userId,
    host?.initData,
    host?.isTelegram,
    host?.platform,
    host?.source,
    isOnline,
    locale,
  ])

  const hasUnsavedChanges = useMemo(() => {
    return hasUnsavedSettingsChanges(snapshot, {
      locale: localeDraft,
      saveMode: saveModeDraft,
      storageMode: storageModeDraft,
      themeMode: themeModeDraft,
      aiProvider: aiProviderDraft,
      aiModel: aiModelDraft,
      apiKeyDraft,
      removeStoredKeyRequested,
    })
  }, [
    aiModelDraft,
    aiProviderDraft,
    apiKeyDraft,
    localeDraft,
    removeStoredKeyRequested,
    saveModeDraft,
    snapshot,
    storageModeDraft,
    themeModeDraft,
  ])

  useEffect(() => {
    if (host?.isTelegram || !hasUnsavedChanges) {
      return
    }

    function beforeUnloadHandler(event: BeforeUnloadEvent) {
      const prompt = buildUnsavedChangesPrompt(uiLocale)
      event.preventDefault()
      event.returnValue = prompt
    }

    window.addEventListener('beforeunload', beforeUnloadHandler)

    return () => {
      window.removeEventListener('beforeunload', beforeUnloadHandler)
    }
  }, [hasUnsavedChanges, host?.isTelegram, uiLocale])

  useLayoutEffect(() => {
    if (!host?.isTelegram) {
      return
    }

    const webApp = resolveTelegramMiniAppBridge() as TelegramClosingConfirmationWebApp | undefined

    if (!webApp) {
      emitMiniAppDiagnostic({
        eventType: 'confirm.bridge_missing',
        severity: 'warn',
        message: 'Unable to sync closing confirmation because Telegram WebApp bridge is unavailable.',
        isTelegram: true,
        hostSource: host.source,
        platform: host.platform,
        channel: bootstrap?.scope.channel,
        userId: bootstrap?.scope.userId ?? host.userId,
        conversationId: bootstrap?.scope.conversationId ?? host.conversationId,
        hasInitData: host.initData.length > 0,
        hasWebApp: false,
        locale: localeDraft,
        details: {
          hasUnsavedChanges,
        },
      })
    }

    return syncTelegramClosingConfirmation(webApp, hasUnsavedChanges)
  }, [
    bootstrap?.scope.channel,
    bootstrap?.scope.conversationId,
    bootstrap?.scope.userId,
    bridgeReadyVersion,
    hasUnsavedChanges,
    host,
    localeDraft,
  ])

  useEffect(() => {
    const hostTheme = host?.theme ?? 'light'
    document.documentElement.dataset.theme = resolveAppliedTheme(themeModeDraft, hostTheme)

    return () => {
      document.documentElement.dataset.theme = resolveAppliedTheme(persistedThemeMode, hostTheme)
    }
  }, [host?.theme, persistedThemeMode, themeModeDraft])

  const applyCommittedSettings = useCallback((response: MiniAppSettingsCommitResponse) => {
    const normalizedLocale = normalizeLocaleFromPreference(response.locale)
    const normalizedThemeMode = normalizeThemeMode(response.themeMode)
    setLocaleInStore(normalizedLocale)
    setThemeModeInStore(normalizedThemeMode)
    setLocaleDraft(normalizedLocale)
    setSaveModeDraft(response.saveMode)
    setStorageModeDraft(response.storageMode)
    setThemeModeDraft(normalizedThemeMode)
    setAiProviderDraft(response.aiProvider)
    setAiProviders(response.availableProviders)
    setAiModelDraft(response.aiModel)
    setAiModels(response.availableModels)
    setKeyStatus({
      hasStoredKey: response.hasStoredKey,
      source: response.apiKeySource,
    })
    setBootstrapPreferences({
      saveMode: response.saveMode,
      availableSaveModes: response.availableSaveModes,
      storageMode: response.storageMode,
      availableStorageModes: response.availableStorageModes,
    })
    setBootstrapSettings({
      aiProvider: response.aiProvider,
      availableProviders: response.availableProviders,
      aiModel: response.aiModel,
      availableModels: response.availableModels,
      hasStoredKey: response.hasStoredKey,
      apiKeySource: response.apiKeySource,
      themeMode: normalizedThemeMode,
      availableThemeModes: response.availableThemeModes,
    })
    setApiKeyDraft('')
    setRemoveStoredKeyRequested(false)
    setSnapshot({
      locale: normalizedLocale,
      saveMode: response.saveMode,
      storageMode: response.storageMode,
      themeMode: normalizedThemeMode,
      aiProvider: response.aiProvider,
      aiModel: response.aiModel,
    })
  }, [setBootstrapPreferences, setBootstrapSettings, setLocaleInStore, setThemeModeInStore])

  useEffect(() => {
    if (!bootstrap || !aiProviderDraft) {
      return
    }

    if (skipInitialProviderRefreshRef.current) {
      skipInitialProviderRefreshRef.current = false
      return
    }

    const requestVersion = ++providerRequestVersion.current
    setModelsLoading(true)

    void (async () => {
      try {
        const [modelResponse, keyResponse] = await Promise.all([
          getAiModel(scopedUserId, aiProviderDraft, scopedConversationId),
          getAiKeyStatus(scopedUserId, aiProviderDraft, scopedConversationId),
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
  }, [aiProviderDraft, bootstrap, scopedConversationId, scopedUserId])

  if (!bootstrap || !policy) {
    return <div className="card">{copy.noBootstrap}</div>
  }

  const activeGraphStatus = graphStatus ?? bootstrap.graph
  const storageLocked = policy.allowedStorageModes.length === 1
  const storageOptions = storageLocked
    ? policy.allowedStorageModes
    : bootstrap.preferences.availableStorageModes
  const bootstrapChannel = bootstrap.scope.channel
  const bootstrapConversationId = bootstrap.scope.conversationId
  const availableSaveModes = bootstrap.preferences.availableSaveModes

  const oneDriveConnected = activeGraphStatus.isAuthenticated
  const languageChoices = buildLanguageChoices(uiLocale)
  const themeChoices = buildThemeChoices(uiLocale)
    .filter((choice) => bootstrap.settings.availableThemeModes.includes(choice.value))
  const saveModeChoices = buildSaveModeChoices(uiLocale, availableSaveModes)
  const storageChoices = buildStorageModeChoices(uiLocale, storageOptions)
  const providerChoices = aiProviders.map((provider) => ({
    value: provider,
    label: formatProviderLabel(provider),
  }))
  const modelChoices = aiModels.map((model) => ({
    value: model,
    title: formatModelLabel(model),
    description: model,
  }))
  const oneDriveVisual = resolveOneDriveVisual(
    {
      isConfigured: activeGraphStatus.isConfigured,
      isAuthenticated: activeGraphStatus.isAuthenticated,
    },
    uiLocale,
  )
  const notionVisual = resolveNotionVisual(
    {
      vocabularyEnabled: integrationStatus?.notionVocabulary.enabled ?? false,
      vocabularyConfigured: integrationStatus?.notionVocabulary.isConfigured ?? false,
      foodEnabled: integrationStatus?.notionFood.enabled ?? false,
      foodConfigured: integrationStatus?.notionFood.isConfigured ?? false,
    },
    uiLocale,
  )
  const keyMetaItems = [
    `${copy.keySourceLabel}: ${mapAiKeySource(keyStatus.source, uiLocale)}`,
    `${copy.keyStoredLabel}: ${keyStatus.hasStoredKey ? copy.yes : copy.no}`,
    `${copy.modelCountLabel}: ${aiModels.length}${modelsLoading ? ` (${copy.loadingModels})` : ''}`,
  ]
  const generalHeadline = uiLocale === 'uk' ? 'Мова та збереження' : 'Language and persistence'
  const aiHeadline = uiLocale === 'uk' ? 'AI конфігурація' : 'AI configuration'
  const integrationsHeadline = uiLocale === 'uk' ? 'Підключені сервіси' : 'Connected services'

  async function runAction(task: () => Promise<void>, successMessage: string): Promise<boolean> {
    if (!isOnline) {
      setSaveStatus(copy.offlineError)
      return false
    }

    setSaving(true)
    setSaveStatus(null)

    try {
      await task()
      setSaveStatus(successMessage)
      return true
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Operation failed'
      setSaveStatus(`${copy.errorPrefix}: ${message}`)
      return false
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

    const commitRequest = {
      locale: localeDraft,
      saveMode: saveModeDraft,
      storageMode: storageModeDraft,
      themeMode: themeModeDraft,
      aiProvider: aiProviderDraft,
      aiModel: aiModelDraft,
      apiKey: apiKeyDraft.trim().length > 0 ? apiKeyDraft.trim() : null,
      removeStoredKey: removeStoredKeyRequested && apiKeyDraft.trim().length === 0,
    }
    setSaving(true)
    setSaveStatus(null)

    try {
      emitMiniAppDiagnostic({
        eventType: 'settings.save_start',
        severity: 'info',
        message: 'Saving settings from Mini App.',
        isTelegram: host?.isTelegram,
        hostSource: host?.source,
        platform: host?.platform,
        channel: bootstrapChannel,
        userId: scopedUserId ?? host?.userId,
        conversationId: bootstrapConversationId,
        hasInitData: Boolean(host?.initData),
        hasWebApp: Boolean(resolveTelegramMiniAppBridge()),
        locale: localeDraft,
        details: {
          saveMode: saveModeDraft,
          storageMode: storageModeDraft,
          themeMode: themeModeDraft,
          aiProvider: aiProviderDraft,
          aiModel: aiModelDraft,
        },
      })

      const response = await commitMiniAppSettings({
        ...commitRequest,
        selectedManually: true,
        channel: bootstrapChannel,
        userId: scopedUserId ?? undefined,
        conversationId: bootstrapConversationId,
        initData: host?.initData || undefined,
      })
      applyCommittedSettings(response)
      emitMiniAppDiagnostic({
        eventType: 'settings.save_success',
        severity: 'info',
        message: 'Settings persisted successfully.',
        isTelegram: host?.isTelegram,
        hostSource: host?.source,
        platform: host?.platform,
        channel: bootstrapChannel,
        userId: scopedUserId ?? host?.userId,
        conversationId: bootstrapConversationId,
        hasInitData: Boolean(host?.initData),
        hasWebApp: Boolean(resolveTelegramMiniAppBridge()),
        locale: response.locale,
        details: {
          responseLocale: response.locale,
          responseThemeMode: response.themeMode,
          responseProvider: response.aiProvider,
          responseModel: response.aiModel,
        },
      })
      setSaveStatus(copy.saveSuccess)
      const webApp = await waitForTelegramMiniAppBridge()
      applyTelegramClosingConfirmation(webApp, false)
      await new Promise<void>((resolve) => window.setTimeout(resolve, 32))
      const closeSucceeded = closeTelegramMiniApp(webApp)
      emitMiniAppDiagnostic({
        eventType: closeSucceeded ? 'settings.close_success' : 'settings.close_failed',
        severity: closeSucceeded ? 'info' : 'warn',
        message: closeSucceeded ? 'Requested Telegram Mini App close after save.' : 'Failed to request Telegram Mini App close after save.',
        isTelegram: host?.isTelegram,
        hostSource: host?.source,
        platform: host?.platform,
        channel: bootstrapChannel,
        userId: scopedUserId ?? host?.userId,
        conversationId: bootstrapConversationId,
        hasInitData: Boolean(host?.initData),
        hasWebApp: Boolean(webApp),
        locale: response.locale,
      })
      if (!closeSucceeded) {
        setSaveStatus(copy.saveSuccess)
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Operation failed'
      emitMiniAppDiagnostic({
        eventType: 'settings.save_failure',
        severity: 'error',
        message,
        isTelegram: host?.isTelegram,
        hostSource: host?.source,
        platform: host?.platform,
        channel: bootstrapChannel,
        userId: scopedUserId ?? host?.userId,
        conversationId: bootstrapConversationId,
        hasInitData: Boolean(host?.initData),
        hasWebApp: Boolean(resolveTelegramMiniAppBridge()),
        locale: localeDraft,
      })
      setSaveStatus(`${copy.errorPrefix}: ${message}`)
    } finally {
      setSaving(false)
    }
  }

  async function handleRefreshStatus() {
    if (!isOnline) {
      setSaveStatus(copy.offlineError)
      setLoadError(copy.offlineError)
      return
    }

    setSaving(true)
    setSaveStatus(null)
    setLoadError(null)

    try {
      const [notionStatus, currentGraphStatus] = await Promise.all([
        getIntegrationNotionStatus(),
        getGraphStatus(),
      ])
      setIntegrationStatus(notionStatus)
      setGraphStatus(currentGraphStatus)
      setSaveStatus(uiLocale === 'en' ? 'Status refreshed.' : 'Статус оновлено.')
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Refresh failed'
      setLoadError(message)
      setSaveStatus(`${copy.errorPrefix}: ${message}`)
    } finally {
      setSaving(false)
    }
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
      <section className="tg-profile-card settings-hero">
        <div className="tg-profile-main">
          <div className="tg-avatar" aria-hidden="true">
            <ChoiceIcon icon="hero-settings" />
          </div>
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
          <button type="button" className="btn-secondary" onClick={() => void handleRefreshStatus()} disabled={saving}>
            {copy.retry}
          </button>
        </section>
      )}

      <section className="tg-card settings-section">
        <div className="settings-section__intro">
          <p className="settings-section__eyebrow">{copy.generalSection}</p>
          <h3 className="tg-section-title">{generalHeadline}</h3>
          <p className="settings-section__description">{copy.generalIntro}</p>
        </div>

        <div className="settings-subsection">
          <div className="settings-subsection__header">
            <div>
              <h4 className="settings-subsection__title">{copy.languageLabel}</h4>
              <p className="settings-subsection__hint">{copy.languageHint}</p>
            </div>
          </div>
          <ChoiceGrid
            options={languageChoices}
            value={localeDraft}
            onChange={(value) => setLocaleDraft(value === 'en' ? 'en' : 'uk')}
            disabled={saving || !isOnline}
            columns="compact"
          />
        </div>

        <div className="settings-subsection">
          <div className="settings-subsection__header">
            <div>
              <h4 className="settings-subsection__title">{copy.themeLabel}</h4>
              <p className="settings-subsection__hint">{copy.themeHint}</p>
            </div>
          </div>
          <ChoiceGrid
            options={themeChoices}
            value={themeModeDraft}
            onChange={(value) => setThemeModeDraft(normalizeThemeMode(value))}
            disabled={saving || !isOnline}
            columns="compact"
          />
        </div>

        <div className="settings-subsection">
          <div className="settings-subsection__header">
            <div>
              <h4 className="settings-subsection__title">{copy.saveModeLabel}</h4>
              <p className="settings-subsection__hint">{copy.saveModeHint}</p>
            </div>
          </div>
          <ChoiceGrid
            options={saveModeChoices}
            value={saveModeDraft}
            onChange={setSaveModeDraft}
            disabled={saving || !isOnline}
          />
        </div>

        <div className="settings-subsection">
          <div className="settings-subsection__header">
            <div>
              <h4 className="settings-subsection__title">{copy.storageModeLabel}</h4>
              <p className="settings-subsection__hint">{copy.storageModeHint}</p>
            </div>
            {storageLocked && <StatusChip tone="warn">{mapStorageMode(storageModeDraft, uiLocale)}</StatusChip>}
          </div>
          <ChoiceGrid
            options={storageChoices}
            value={storageModeDraft}
            onChange={setStorageModeDraft}
            disabled={saving || !isOnline || storageLocked}
            columns="compact"
          />
          {storageLocked && <p className="tg-helper">{copy.storageLockedHint}</p>}
        </div>
      </section>

      <section className="tg-card settings-section">
        <div className="settings-section__intro">
          <p className="settings-section__eyebrow">{copy.aiSection}</p>
          <h3 className="tg-section-title">{aiHeadline}</h3>
          <p className="settings-section__description">{copy.aiIntro}</p>
        </div>

        {loading && <p className="tg-helper">{copy.loadingAi}</p>}

        {!loading && (
          <>
            <div className="settings-subsection">
              <div className="settings-subsection__header">
                <div>
                  <h4 className="settings-subsection__title">{copy.providerLabel}</h4>
                  <p className="settings-subsection__hint">{copy.providerHint}</p>
                </div>
              </div>
              <ChoiceChipGroup
                options={providerChoices}
                value={aiProviderDraft}
                onChange={setAiProviderDraft}
                disabled={saving || !isOnline}
              />
            </div>

            <div className="settings-subsection">
              <div className="settings-subsection__header">
                <div>
                  <h4 className="settings-subsection__title">{copy.modelLabel}</h4>
                  <p className="settings-subsection__hint">{copy.modelHint}</p>
                </div>
                <StatusChip tone={modelsLoading ? 'warn' : 'ok'}>
                  {modelsLoading ? copy.loadingModels : `${aiModels.length}`}
                </StatusChip>
              </div>
              <ChoiceGrid
                options={modelChoices}
                value={aiModelDraft}
                onChange={setAiModelDraft}
                disabled={saving || !isOnline || modelsLoading}
              />
            </div>

            <div className="settings-subsection">
              <div className="settings-subsection__header">
                <div>
                  <h4 className="settings-subsection__title">{copy.apiKeyLabel}</h4>
                  <p className="settings-subsection__hint">{copy.apiKeyHint}</p>
                </div>
              </div>

              <label className="field-shell" htmlFor="api-key-input">
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

              <label className="tg-check tg-check--boxed">
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

              <div className="info-pill-row">
                {keyMetaItems.map((item) => (
                  <span className="info-pill" key={item}>{item}</span>
                ))}
              </div>
            </div>
          </>
        )}
      </section>

      <section className="tg-card settings-section">
        <div className="settings-section__split">
          <div className="settings-section__intro settings-section__intro--tight">
            <p className="settings-section__eyebrow">{copy.integrationsSection}</p>
            <h3 className="tg-section-title">{integrationsHeadline}</h3>
            <p className="settings-section__description">{copy.integrationsIntro}</p>
          </div>

          <button
            type="button"
            className="btn-secondary btn-secondary--quiet"
            onClick={handleRefreshStatus}
            disabled={saving || !isOnline}
          >
            {copy.refreshStatus}
          </button>
        </div>

        {!integrationStatus && <p className="tg-helper">{copy.loadingIntegrations}</p>}

        <div className="integration-stack">
          <IntegrationCard
            icon={<ChoiceIcon icon="service-cloud" />}
            title={copy.oneDriveStatusLabel}
            subtitle={`${copy.oneDriveSubtitle} ${oneDriveVisual.description}`}
            tone={oneDriveVisual.tone}
            status={oneDriveVisual.label}
            facts={[
              {
                label: copy.oneDriveTokenLabel,
                value: formatDateTime(activeGraphStatus.accessTokenExpiresAtUtc, uiLocale, copy.noData),
              },
              {
                label: copy.storageModeLabel,
                value: mapStorageMode(storageModeDraft, uiLocale),
              },
            ]}
            actions={(
              <>
                {!oneDriveConnected && (
                  <>
                    <ActionTile
                      title={copy.startLogin}
                      hint={copy.startLoginHint}
                      onClick={handleStartOneDriveLogin}
                      disabled={saving || !isOnline}
                    />
                    <ActionTile
                      title={copy.finishLogin}
                      hint={copy.finishLoginHint}
                      onClick={handleCompleteOneDriveLogin}
                      disabled={saving || !isOnline || !loginChallenge}
                    />
                  </>
                )}
                {oneDriveConnected && (
                  <ActionTile
                    title={copy.logout}
                    hint={copy.logoutHint}
                    tone="danger"
                    onClick={handleLogoutOneDrive}
                    disabled={saving || !isOnline}
                  />
                )}
              </>
            )}
          >
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

            <details className="details-block details-block--soft">
              <summary>{copy.serviceActions}</summary>
              <div className="details-content">
                <p className="tg-helper tg-helper--compact">{copy.serviceActionsHint}</p>
                <div className="integration-actions integration-actions--maintenance">
                  <ActionTile
                    title={copy.syncNow}
                    hint={copy.syncNowHint}
                    onClick={handleSyncNow}
                    disabled={saving || !isOnline}
                  />
                  <ActionTile
                    title={copy.rebuildIndex}
                    hint={copy.rebuildIndexHint}
                    onClick={handleRebuildIndex}
                    disabled={saving || !isOnline}
                  />
                  <ActionTile
                    title={copy.clearCache}
                    hint={copy.clearCacheHint}
                    tone="danger"
                    onClick={handleClearCache}
                    disabled={saving || !isOnline}
                  />
                </div>
              </div>
            </details>
          </IntegrationCard>

          <IntegrationCard
            icon="N"
            title={copy.notionLabel}
            subtitle={`${copy.notionSubtitle} ${notionVisual.description}`}
            tone={notionVisual.tone}
            status={notionVisual.label}
            facts={[
              {
                label: copy.notionVocabularyLabel,
                value: integrationStatus?.notionVocabulary.enabled
                  ? copy.enabled
                  : integrationStatus?.notionVocabulary.isConfigured
                    ? copy.disabled
                    : copy.notConfigured,
              },
              {
                label: copy.notionFoodLabel,
                value: integrationStatus?.notionFood.enabled
                  ? copy.enabled
                  : integrationStatus?.notionFood.isConfigured
                    ? copy.disabled
                    : copy.notConfigured,
              },
            ]}
          />
        </div>
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



