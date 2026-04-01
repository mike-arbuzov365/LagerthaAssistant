// Design tokens — source of truth (docs/08-design-system.md)
// CSS custom properties are declared in index.css and consumed here as constants for TS use.

export const color = {
  bgPrimary: '#0F0F0F',
  bgSurface: '#1A1A1A',
  bgElevated: '#242424',
  bgInput: '#1F1F1F',

  accentGold: '#D4AF37',
  accentGoldDim: '#B8942E',
  accentAi: '#7C4DFF',
  accentAiDim: '#6B3FE0',
  accentClient: '#2D87FF',

  success: '#34C759',
  error: '#FF3B30',
  warning: '#FF9F0A',
  info: '#5AC8FA',

  textPrimary: '#FFFFFF',
  textSecondary: 'rgba(255,255,255,0.60)',
  textTertiary: 'rgba(255,255,255,0.55)',
  textInverse: '#0F0F0F',

  borderDefault: 'rgba(255,255,255,0.08)',
  borderFocus: 'rgba(212,175,55,0.60)',
  borderError: 'rgba(255,59,48,0.60)',
} as const

export const space = {
  s1: '4px',
  s2: '8px',
  s3: '12px',
  s4: '16px',
  s5: '20px',
  s6: '24px',
  s8: '32px',
  s10: '40px',
  s12: '48px',
} as const

export const radius = {
  sm: '8px',
  md: '12px',
  lg: '16px',
  xl: '20px',
  full: '9999px',
} as const
