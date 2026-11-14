export const SESSION_KEY = 'kreatx.session'
const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'

// Leximi i JWT ndihmon pamjen; vetem API verifikon nenshkrimin dhe autorizimin.
export function parseSession(value, now = Date.now()) {
  try {
    const parts = value.token.split('.')
    if (parts.length !== 3 || parts.some((part) => !/^[A-Za-z0-9_-]+$/.test(part))) return null
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0))
    const claims = JSON.parse(new TextDecoder().decode(bytes))
    const roles = Array.isArray(claims[ROLE_CLAIM]) ? claims[ROLE_CLAIM] : [claims[ROLE_CLAIM]]
    const expiresAt = Math.min(claims.exp * 1000, Date.parse(value.expiresAt))
    if (!Number.isFinite(expiresAt) || expiresAt <= now || (claims.nbf && claims.nbf * 1000 > now) ||
      !claims.sub || claims.sub !== value.user.id ||
      !roles.some((role) => role === 'Administrator' || role === 'Employee')) return null
    return { token: value.token, expiresAt: new Date(expiresAt).toISOString(), user: { ...value.user, roles } }
  } catch {
    return null
  }
}

export function readSession() {
  try {
    const stored = sessionStorage.getItem(SESSION_KEY)
    return stored ? parseSession(JSON.parse(stored)) : null
  } catch {
    return null
  }
}
