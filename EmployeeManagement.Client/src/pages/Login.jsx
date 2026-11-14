import { useState } from 'react'
import { useAuth } from '../auth/useAuth'
import Brand from '../components/Brand'
import Icon from '../components/Icon'

export default function Login({ message }) {
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(event) {
    event.preventDefault()
    if (busy) return
    setError('')
    setBusy(true)
    try { await login(email.trim(), password) }
    catch (failure) { setError(failure.message) }
    finally { setBusy(false) }
  }

  return <main className="login-screen">
    <aside className="login-story">
      <Brand />
      <div className="login-story-content">
        <h2>Your work, in one place.</h2>
        <p>Access your projects, tasks and profile in one simple workspace.</p>
        <div className="login-topics" aria-label="Workspace sections">
          <span><Icon name="users" size={18} />People</span>
          <span><Icon name="projects" size={18} />Projects</span>
          <span><Icon name="tasks" size={18} />Tasks</span>
        </div>
      </div>
    </aside>
    <div className="login-panel">
      <div className="login-mobile-brand"><Brand /></div>
      <section className="login-card" aria-labelledby="login-title">
        <span className="login-form-icon"><Icon name="lock" size={22} /></span>
        <h1 id="login-title">Welcome back</h1>
        <p className="muted">Use your work account to continue.</p>
        {(error || message) && <p className="error" role="alert"><Icon name="info" size={18} /><span>{error || message}</span></p>}
        <form onSubmit={submit} aria-busy={busy}>
          <label htmlFor="email">Email</label>
          <div className="input-wrap"><Icon name="mail" size={18} /><input id="email" type="email" placeholder="you@company.com" autoComplete="username" required value={email} onChange={(event) => setEmail(event.target.value)} disabled={busy} /></div>
          <label htmlFor="password">Password</label>
          <div className="input-wrap"><Icon name="lock" size={18} /><input id="password" type="password" placeholder="Enter your password" autoComplete="current-password" required value={password} onChange={(event) => setPassword(event.target.value)} disabled={busy} /></div>
          <button className="primary" disabled={busy}>{busy ? <><span className="spinner" />Signing in…</> : <>Sign in<Icon name="arrow" size={17} /></>}</button>
        </form>
        <p className="login-help">Need access? Contact your administrator.</p>
      </section>
    </div>
  </main>
}
