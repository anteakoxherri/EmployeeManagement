import { useEffect, useState } from 'react'
import { AuthProvider } from './auth/AuthProvider'
import { useAuth } from './auth/useAuth'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Profile from './pages/Profile'
import Users from './pages/Users'
import Projects from './pages/Projects'
import Tasks from './pages/Tasks'
import Layout from './components/Layout'
import './App.css'

function Router() {
  const { session, checking, message } = useAuth()
  const [path, setPath] = useState(() => window.location.hash.slice(1) || '/')
  const administrator = session?.user.roles.includes('Administrator')
  const dashboard = administrator ? '/administrator/dashboard' : '/employee/dashboard'
  const allowed = [dashboard, '/projects', '/tasks', '/profile', ...(administrator ? ['/users'] : [])]
  const routePath = path.split('?')[0]
  const destination = session ? (routePath === '/tasks' ? path : allowed.includes(path) ? path : dashboard) : '/login'
  const page = destination.split('?')[0]

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
  const title = page === dashboard ? 'Dashboard' : page.slice(1).replace(/^./, (letter) => letter.toUpperCase())
  return <Layout dashboard={dashboard} path={page} title={title}>
    {page === dashboard ? <Dashboard /> : page === '/profile' ? <Profile /> : page === '/users' ? <Users /> : page === '/projects' ? <Projects /> : <Tasks key={session.user.id} projectFilter={new URLSearchParams(destination.split('?')[1]).get('projectId') || ''} />}
  </Layout>
}

export default function App() {
  return <AuthProvider><Router /></AuthProvider>
}
