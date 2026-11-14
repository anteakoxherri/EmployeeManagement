import { useCallback, useEffect, useState } from 'react'
import { apiRequest } from '../api/client'
import { AuthContext } from './useAuth'
import { parseSession, readSession, SESSION_KEY } from './session'

export function AuthProvider({ children }) {
  const [session, setSession] = useState(readSession)
  const [checking, setChecking] = useState(true)
  const [message, setMessage] = useState('')

  const logout = useCallback((reason = '') => {
    try { sessionStorage.removeItem(SESSION_KEY) } catch { /* Sesioni ne memorie pastrohet gjithsesi. */ }
    setSession(null)
    setMessage(reason)
  }, [])

  useEffect(() => {
    const saved = readSession()
    const controller = new AbortController()
    async function restore() {
      try {
        if (saved) {
          // API verifikon JWT e ruajtur para hapjes se faqeve te mbrojtura.
          await apiRequest('/profile/me', { token: saved.token, signal: controller.signal })
        } else {
          let hadSession = false
          try { hadSession = Boolean(sessionStorage.getItem(SESSION_KEY)) } catch { /* Ruajtja mund te jete e bllokuar. */ }
          logout(hadSession ? 'Your session is invalid or expired. Please sign in again.' : '')
        }
      } catch (error) {
        if (error.name === 'AbortError') return
        logout(error.message)
      } finally {
        if (!controller.signal.aborted) setChecking(false)
      }
    }
    restore()
    return () => controller.abort()
  }, [logout])

  useEffect(() => {
    if (!session) return
    const expire = () => logout('Your session has expired. Please sign in again.')
    const remaining = Date.parse(session.expiresAt) - Date.now()
    const timer = window.setTimeout(expire, Math.max(0, remaining))
    const checkExpiry = () => { if (Date.parse(session.expiresAt) <= Date.now()) expire() }
    window.addEventListener('focus', checkExpiry)
    document.addEventListener('visibilitychange', checkExpiry)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener('focus', checkExpiry)
      document.removeEventListener('visibilitychange', checkExpiry)
    }
  }, [session, logout])

  async function login(email, password) {
    const response = await apiRequest('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) })
    const nextSession = parseSession(response)
    if (!nextSession) throw new Error('The API returned an invalid session or an unsupported role.')
    await apiRequest('/profile/me', { token: nextSession.token })
    if (Date.parse(nextSession.expiresAt) <= Date.now()) throw new Error('Your session has expired. Please sign in again.')
    // sessionStorage ruan sesionin gjate rifreskimit, pa ruajtur password-in.
    try { sessionStorage.setItem(SESSION_KEY, JSON.stringify(nextSession)) } catch { /* Vazhdojme me sesionin ne memorie. */ }
    setMessage('')
    setSession(nextSession)
  }

  // Per kerkesat e fazes tjeter perdoret i njejti trajtim i 401.
  const token = session?.token
  const request = useCallback((path, options) => apiRequest(path, {
    ...options, token,
    onUnauthorized: () => logout('Your session is invalid or expired. Please sign in again.'),
  }), [token, logout])

  // Rifreskojme vetem emrin ne pamje; token-i dhe rolet mbeten te pandryshuara.
  const updateProfile = useCallback((profile) => {
    setSession((current) => {
      if (!current) return current
      const next = { ...current, user: { ...current.user, firstName: profile.firstName, lastName: profile.lastName } }
      try { sessionStorage.setItem(SESSION_KEY, JSON.stringify(next)) } catch { /* Sesioni vazhdon ne memorie. */ }
      return next
    })
  }, [])

  return <AuthContext.Provider value={{ session, checking, message, login, logout, request, updateProfile }}>{children}</AuthContext.Provider>
}
