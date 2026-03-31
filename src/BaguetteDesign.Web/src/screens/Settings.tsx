import { useState, useCallback, useRef, useEffect } from 'react'
import './Settings.css'

// ── TMA SDK type shim ──────────────────────────────────────────────────────
declare global {
  interface Window {
    Telegram?: {
      WebApp: {
        ready(): void
        expand(): void
        enableClosingConfirmation(): void
        contentSafeAreaInsets?: { top: number }
        HapticFeedback?: { notificationOccurred(type: string): void }
      }
    }
  }
}

// ── Types ──────────────────────────────────────────────────────────────────

type Role = 'designer' | 'client'
type Language = 'uk' | 'en'
type Currency = 'UAH' | 'USD' | 'EUR'
type AiModel = 'Claude' | 'GPT-4o'
type AiTone = 'Чернетка' | 'Стандарт' | 'Дослідження'
type NotifMode = 'Лише критичні' | 'Повна підтримка'
type IntegrationStatus = 'connected' | 'error' | 'not-configured' | 'loading'

interface IntegrationState {
  status: IntegrationStatus
  syncLabel: string
}

// ── Sub-components ─────────────────────────────────────────────────────────

function SectionHeader({ children, action }: { children: React.ReactNode; action?: React.ReactNode }) {
  return (
    <div className="section-header">
      <span>{children}</span>
      {action}
    </div>
  )
}

function Card({ children }: { children: React.ReactNode }) {
  return <div className="card">{children}</div>
}

function Divider() {
  return <div className="divider" />
}

function Segmented<T extends string>({
  options, value, onChange, ariaLabel,
}: {
  options: T[]
  value: T
  onChange: (v: T) => void
  ariaLabel?: string
}) {
  return (
    <div className="segmented" role="group" aria-label={ariaLabel}>
      {options.map(opt => (
        <button
          key={opt}
          type="button"
          className={`segmented__option${value === opt ? ' segmented__option--active' : ''}`}
          onClick={() => onChange(opt)}
        >
          {opt}
        </button>
      ))}
    </div>
  )
}

function Toggle({ checked, onChange }: { checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="toggle">
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} />
      <span className="toggle__track">
        <span className="toggle__thumb" />
      </span>
    </label>
  )
}

function ToggleRow({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="toggle-wrapper">
      <span className="toggle-wrapper__label">{label}</span>
      <Toggle checked={checked} onChange={onChange} />
    </label>
  )
}

const BADGE_LABELS: Record<IntegrationStatus, string> = {
  connected: 'Підключено',
  error: 'Помилка',
  'not-configured': 'Не налаштовано',
  loading: '…',
}

function StatusBadge({ status }: { status: IntegrationStatus }) {
  return (
    <span className={`badge badge--${status}`}>
      {status === 'loading'
        ? <span className="badge__spinner" />
        : <span className="badge__dot" />
      }
      {BADGE_LABELS[status]}
    </span>
  )
}

function IntegrationRow({
  icon, name, syncLabel, status, onClick,
}: {
  icon: string
  name: string
  syncLabel: string
  status: IntegrationStatus
  onClick(): void
}) {
  return (
    <div className="integration-row" onClick={onClick} role="button" tabIndex={0}
      onKeyDown={e => e.key === 'Enter' && onClick()}>
      <div className="integration-logo">{icon}</div>
      <div className="integration-info">
        <div className="integration-name">{name}</div>
        <div className="integration-sync">{syncLabel}</div>
      </div>
      <div className="integration-right">
        <StatusBadge status={status} />
        <span className="list-row__chevron">›</span>
      </div>
    </div>
  )
}

function AISlider({ value, onChange }: { value: number; onChange(v: number): void }) {
  const positions = [0, 50, 100]
  const labels = ['Чернетка', 'Стандарт', 'Дослідження']
  const pct = positions[value]

  return (
    <div className="ai-slider" onClick={() => onChange((value + 1) % 3)} role="slider"
      aria-valuenow={value} aria-valuemin={0} aria-valuemax={2}
      tabIndex={0} onKeyDown={e => e.key === 'Enter' && onChange((value + 1) % 3)}>
      <div className="ai-slider__track">
        <div className="ai-slider__fill" style={{ width: `${pct}%` }} />
        <div className="ai-slider__thumb" style={{ left: `${pct}%` }} />
      </div>
      <div className="ai-slider__labels">
        {labels.map((l, i) => (
          <span key={l} style={{ fontWeight: i === value ? 600 : 400, color: i === value ? 'var(--color-accent-ai)' : undefined }}>
            {l}
          </span>
        ))}
      </div>
    </div>
  )
}

function ListRow({
  icon, label, value, onClick, labelStyle,
}: {
  icon?: string
  label: string
  value?: string
  onClick?: () => void
  labelStyle?: React.CSSProperties
}) {
  return (
    <div className="list-row" onClick={onClick} style={onClick ? { cursor: 'pointer' } : undefined}
      role={onClick ? 'button' : undefined} tabIndex={onClick ? 0 : undefined}
      onKeyDown={onClick ? (e => e.key === 'Enter' && onClick()) : undefined}>
      <div className="list-row__left">
        {icon && <div className="list-row__icon">{icon}</div>}
        <span className="list-row__label" style={labelStyle}>{label}</span>
      </div>
      <div className="list-row__right">
        {value && <span className="list-row__value">{value}</span>}
        {onClick && <span className="list-row__chevron">›</span>}
      </div>
    </div>
  )
}

function InputField({
  id, label, type = 'text', placeholder, value, onChange,
}: {
  id: string
  label: string
  type?: string
  placeholder?: string
  value: string
  onChange(v: string): void
}) {
  return (
    <div>
      <label className="input-label" htmlFor={id}>{label}</label>
      <div className="input-field">
        <input id={id} type={type} placeholder={placeholder} value={value}
          onChange={e => onChange(e.target.value)} />
      </div>
    </div>
  )
}

function BottomSheet({
  id, title, visible, onClose, children,
}: {
  id: string
  title: string
  visible: boolean
  onClose(): void
  children: React.ReactNode
}) {
  return (
    <>
      <div
        className={`overlay${visible ? ' overlay--visible' : ''}`}
        onClick={onClose}
      />
      <div
        id={id}
        className={`bottom-sheet${visible ? ' bottom-sheet--visible' : ''}`}
      >
        <div className="bottom-sheet__handle" />
        <div className="bottom-sheet__title">{title}</div>
        {children}
      </div>
    </>
  )
}

function SheetOption({
  label, selected, onClick,
}: { label: string; selected: boolean; onClick(): void }) {
  return (
    <div
      className={`bottom-sheet__option${selected ? ' bottom-sheet__option--selected' : ''}`}
      onClick={onClick}
      role="option"
      aria-selected={selected}
      tabIndex={0}
      onKeyDown={e => e.key === 'Enter' && onClick()}
    >
      <span>{label}</span>
      <span className="bottom-sheet__check" style={{ opacity: selected ? 1 : 0 }}>✓</span>
    </div>
  )
}

// ── Toast ──────────────────────────────────────────────────────────────────

function useToast() {
  const [toast, setToast] = useState<{ msg: string; success: boolean } | null>(null)
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)

  const show = useCallback((msg: string, success = false) => {
    if (timer.current) clearTimeout(timer.current)
    setToast({ msg, success })
    timer.current = setTimeout(() => setToast(null), 2200)
  }, [])

  return { toast, show }
}

function Toast({ toast }: { toast: { msg: string; success: boolean } | null }) {
  return (
    <div className={`toast${toast ? ' toast--visible' : ''}${toast?.success ? ' toast--success' : ''}`}>
      {toast?.msg}
    </div>
  )
}

// ── Main screen ────────────────────────────────────────────────────────────

export function Settings() {
  const tg = window.Telegram?.WebApp

  useEffect(() => {
    if (tg) {
      tg.ready()
      tg.expand()
      tg.enableClosingConfirmation()
      const safeTop = tg.contentSafeAreaInsets?.top
      if (safeTop !== undefined) {
        document.documentElement.style.setProperty('--tma-safe-top', `${safeTop}px`)
      }
    }
  }, [])

  // State
  const [role, setRole] = useState<Role>('designer')
  const [lang, setLang] = useState<Language>('uk')
  const [currency, setCurrency] = useState<Currency>('UAH')
  const [aiModel, setAiModel] = useState<AiModel>('Claude')
  const [aiTone, setAiTone] = useState<AiTone>('Стандарт')
  const [creativity, setCreativity] = useState(1)
  const [notifMode, setNotifMode] = useState<NotifMode>('Повна підтримка')
  const [notifNewLead, setNotifNewLead] = useState(true)
  const [notifDeadline, setNotifDeadline] = useState(true)
  const [notifInactive, setNotifInactive] = useState(false)
  const [portfolioUrl, setPortfolioUrl] = useState('')
  const [workFrom, setWorkFrom] = useState('09:00')
  const [workTo, setWorkTo] = useState('18:00')
  const [apiToken, setApiToken] = useState('')
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const [dirty, setDirty] = useState(false)
  const [saving, setSaving] = useState(false)
  const [langSheetOpen, setLangSheetOpen] = useState(false)
  const [currencySheetOpen, setCurrencySheetOpen] = useState(false)

  const [integrations, setIntegrations] = useState<Record<string, IntegrationState>>({
    notion:   { status: 'connected', syncLabel: 'Оновлено 2 хв тому' },
    drive:    { status: 'error',     syncLabel: 'Помилка синхронізації' },
    calendar: { status: 'not-configured', syncLabel: 'Натисніть, щоб підключити' },
  })

  const { toast, show: showToast } = useToast()

  const markDirty = useCallback(() => setDirty(true), [])

  function resync(key: string) {
    setIntegrations(prev => ({
      ...prev,
      [key]: { ...prev[key], status: 'loading' },
    }))
    setTimeout(() => {
      setIntegrations(prev => ({
        ...prev,
        [key]: { status: 'connected', syncLabel: 'Оновлено щойно' },
      }))
      showToast('✓ Синхронізацію виконано', true)
    }, 1500)
  }

  function handleSave() {
    setSaving(true)
    setTimeout(() => {
      setSaving(false)
      setDirty(false)
      showToast('✓ Зміни збережено', true)
      tg?.HapticFeedback?.notificationOccurred('success')
    }, 800)
  }

  const LANG_LABELS: Record<Language, string> = { uk: 'Українська', en: 'English' }
  const CURRENCY_ICONS: Record<Currency, string> = { UAH: '₴ UAH', USD: '$ USD', EUR: '€ EUR' }
  const CURRENCY_NAMES: Record<Currency, string> = { UAH: 'Гривня', USD: 'Долар', EUR: 'Євро' }

  return (
    <div className="settings-root">
      {/* ── Header ── */}
      <header className="header">
        <span className="header__title">Налаштування студії</span>
        <span
          id="roleBadge"
          className={`role-badge${role === 'client' ? ' role-badge--client' : ''}`}
          onClick={() => { setRole(r => r === 'designer' ? 'client' : 'designer'); markDirty() }}
          role="button"
          tabIndex={0}
          onKeyDown={e => e.key === 'Enter' && setRole(r => r === 'designer' ? 'client' : 'designer')}
        >
          {role === 'designer' ? 'Режим дизайнера' : 'Режим клієнта'}
        </span>
      </header>

      {/* ── Page content ── */}
      <main className="page">

        {/* 1. Мова */}
        <section>
          <SectionHeader>Мова та регіон</SectionHeader>
          <ListRow
            label="Мова інтерфейсу"
            value={LANG_LABELS[lang]}
            onClick={() => setLangSheetOpen(true)}
          />
        </section>

        {/* 2. AI */}
        <section>
          <SectionHeader>Налаштування AI</SectionHeader>
          <Card>
            <div>
              <div className="input-label">Модель</div>
              <Segmented
                options={['Claude', 'GPT-4o'] as AiModel[]}
                value={aiModel}
                onChange={v => { setAiModel(v); markDirty() }}
                ariaLabel="Модель AI"
              />
            </div>
            <Divider />
            <div>
              <div className="input-label">Стиль відповіді</div>
              <Segmented
                options={['Чернетка', 'Стандарт', 'Дослідження'] as AiTone[]}
                value={aiTone}
                onChange={v => { setAiTone(v); markDirty() }}
                ariaLabel="Стиль відповіді"
              />
            </div>
            <Divider />
            <div>
              <div className="input-label">Рівень креативності</div>
              <AISlider value={creativity} onChange={v => { setCreativity(v); markDirty() }} />
            </div>
          </Card>
        </section>

        {/* 3. Інтеграції */}
        <section>
          <SectionHeader>Інтеграції</SectionHeader>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
            <IntegrationRow
              icon="📝" name="Notion"
              syncLabel={integrations.notion.syncLabel}
              status={integrations.notion.status}
              onClick={() => resync('notion')}
            />
            <IntegrationRow
              icon="📁" name="Google Drive"
              syncLabel={integrations.drive.syncLabel}
              status={integrations.drive.status}
              onClick={() => resync('drive')}
            />
            <IntegrationRow
              icon="📅" name="Google Calendar"
              syncLabel={integrations.calendar.syncLabel}
              status={integrations.calendar.status}
              onClick={() => showToast('Відкриваємо налаштування…')}
            />
          </div>
        </section>

        {/* 4. Параметри проєктів */}
        <section>
          <SectionHeader>Параметри проєктів</SectionHeader>
          <Card>
            <InputField
              id="portfolioUrl"
              label="URL портфоліо"
              type="url"
              placeholder="https://behance.net/studio"
              value={portfolioUrl}
              onChange={v => { setPortfolioUrl(v); markDirty() }}
            />
            <Divider />
            <ListRow
              label="Валюта"
              value={currency}
              onClick={() => setCurrencySheetOpen(true)}
            />
            <Divider />
            <div>
              <div className="input-label">Робочі години</div>
              <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'center' }}>
                <div className="input-field" style={{ flex: 1 }}>
                  <input type="text" placeholder="09:00" value={workFrom}
                    onChange={e => { setWorkFrom(e.target.value); markDirty() }} />
                </div>
                <span style={{ color: 'var(--color-text-tertiary)' }}>—</span>
                <div className="input-field" style={{ flex: 1 }}>
                  <input type="text" placeholder="18:00" value={workTo}
                    onChange={e => { setWorkTo(e.target.value); markDirty() }} />
                </div>
              </div>
            </div>
          </Card>
        </section>

        {/* 5. Сповіщення */}
        <section>
          <SectionHeader>Сповіщення</SectionHeader>
          <Card>
            <div>
              <div className="input-label">Режим сповіщень</div>
              <Segmented
                options={['Лише критичні', 'Повна підтримка'] as NotifMode[]}
                value={notifMode}
                onChange={v => { setNotifMode(v); markDirty() }}
                ariaLabel="Режим сповіщень"
              />
            </div>
            <Divider />
            <ToggleRow label="Новий лід"
              checked={notifNewLead}
              onChange={v => { setNotifNewLead(v); markDirty() }} />
            <ToggleRow label="Нагадування про дедлайн"
              checked={notifDeadline}
              onChange={v => { setNotifDeadline(v); markDirty() }} />
            <ToggleRow label="Клієнт не відповідає"
              checked={notifInactive}
              onChange={v => { setNotifInactive(v); markDirty() }} />
          </Card>
        </section>

        {/* 6. Дані та приватність */}
        <section>
          <SectionHeader>Дані та приватність</SectionHeader>
          <ListRow icon="📤" label="Експортувати дані" onClick={() => showToast('📤 Експорт підготовлено')} />
          <ListRow
            icon="🗑️"
            label="Очистити контекст AI"
            labelStyle={{ color: 'var(--color-error)' }}
            onClick={() => {
              if (confirm('Очистити контекст AI? Цю дію не можна скасувати.')) {
                showToast('🗑️ Контекст очищено')
                tg?.HapticFeedback?.notificationOccurred('warning')
              }
            }}
          />
        </section>

        {/* 7. Розширені */}
        <section>
          <div
            className="section-header section-header--clickable"
            onClick={() => setAdvancedOpen(o => !o)}
            role="button"
            tabIndex={0}
            onKeyDown={e => e.key === 'Enter' && setAdvancedOpen(o => !o)}
          >
            <span>Розширені налаштування</span>
            <span className="section-header__chevron" style={{ transform: advancedOpen ? 'rotate(90deg)' : '' }}>›</span>
          </div>
          {advancedOpen ? (
            <Card>
              <InputField
                id="apiToken"
                label="API-токени"
                type="password"
                placeholder="sk-ant-..."
                value={apiToken}
                onChange={v => { setApiToken(v); markDirty() }}
              />
            </Card>
          ) : (
            <div className="advanced-hint">Відкрийте, щоб змінити технічні параметри</div>
          )}
        </section>

        {/* 8. Система */}
        <section>
          <SectionHeader>Система</SectionHeader>
          <ListRow icon="ℹ️" label="Версія" value="1.0.0-m2" />
          <div style={{ marginTop: 'var(--space-3)', display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
            <button className="btn btn--ghost btn--full" onClick={() => showToast('📤 Експорт підготовлено')}>
              📤 Експортувати мої дані
            </button>
            <button
              className="btn btn--destructive btn--full"
              onClick={() => { if (confirm('Видалити акаунт? Усі дані будуть втрачені.')) showToast('Акаунт видалено') }}
            >
              Видалити акаунт
            </button>
          </div>
        </section>

      </main>

      {/* ── Save Bar ── */}
      <div className="save-bar">
        <button
          className="btn btn--primary btn--full"
          style={dirty ? { boxShadow: '0 0 0 2px rgba(212,175,55,0.40), var(--shadow-button)' } : undefined}
          onClick={handleSave}
          disabled={saving}
        >
          {saving ? '…' : 'Зберегти зміни'}
        </button>
      </div>

      {/* ── Toast ── */}
      <Toast toast={toast} />

      {/* ── Language Sheet ── */}
      <BottomSheet id="langSheet" title="Мова інтерфейсу" visible={langSheetOpen} onClose={() => setLangSheetOpen(false)}>
        <SheetOption label="🇺🇦 Українська" selected={lang === 'uk'}
          onClick={() => { setLang('uk'); setLangSheetOpen(false); markDirty() }} />
        <SheetOption label="🇬🇧 English" selected={lang === 'en'}
          onClick={() => { setLang('en'); setLangSheetOpen(false); markDirty() }} />
      </BottomSheet>

      {/* ── Currency Sheet ── */}
      <BottomSheet id="currencySheet" title="Валюта" visible={currencySheetOpen} onClose={() => setCurrencySheetOpen(false)}>
        {(['UAH', 'USD', 'EUR'] as Currency[]).map(c => (
          <SheetOption
            key={c}
            label={`${CURRENCY_ICONS[c]} — ${CURRENCY_NAMES[c]}`}
            selected={currency === c}
            onClick={() => { setCurrency(c); setCurrencySheetOpen(false); markDirty() }}
          />
        ))}
      </BottomSheet>
    </div>
  )
}
