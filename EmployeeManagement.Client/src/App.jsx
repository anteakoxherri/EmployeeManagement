import { useEffect, useState } from 'react'
import { AuthProvider } from './auth/AuthProvider'
import { useAuth } from './auth/useAuth'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Placeholder from './pages/Placeholder'
import Profile from './pages/Profile'
import Layout from './components/Layout'
import './App.css'

function Router() {
  const { session, checking, message } = useAuth()
  const [path, setPath] = useState(() => window.location.hash.slice(1) || '/')
  const administrator = session?.user.roles.includes('Administrator')
  const dashboard = administrator ? '/administrator/dashboard' : '/employee/dashboard'
  const allowed = [dashboard, '/projects', '/tasks', '/profile', ...(administrator ? ['/users'] : [])]
  const destination = session ? (allowed.includes(path) ? path : dashboard) : '/login'

  useEffect(() => {
    const onChange = () => setPath(window.location.hash.slice(1) || '/')
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])

  // Kontrollojme edhe adresen direkte, jo vetem lidhjet ne menu.
  useEffect(() => {
    if (!checking && path !== destination) window.location.replace(`#${destination}`)
  }, [checking, path, destination])

  if (checking) return <main className="loading" role="status"><div className="loading-card"><span className="spinner" /><strong>Preparing your workspace</strong><p>Checking your session…</p></div></main>
  if (!session) return <Login message={message} />
  const title = destination === dashboard ? 'Dashboard' : destination.slice(1).replace(/^./, (letter) => letter.toUpperCase())
  return <Layout dashboard={dashboard} path={destination} title={title}>
    {destination === dashboard ? <Dashboard /> : destination === '/profile' ? <Profile /> : <Placeholder title={title} />}
  </Layout>
}

export default function App() {
  return <AuthProvider><Router /></AuthProvider>
}
