import { useAuth } from '../auth/useAuth'
import Brand from './Brand'
import Icon from './Icon'

export default function Layout({ children, dashboard, path, title }) {
  const { session, logout } = useAuth()
  const administrator = session.user.roles.includes('Administrator')
  const { user } = session
  const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ') || user.email || 'Your account'
  const role = administrator ? 'Administrator' : 'Employee'
  const links = [[dashboard, 'Dashboard'], ...(administrator ? [['/users', 'Users']] : []), ['/projects', 'Projects'], ['/tasks', 'Tasks'], ['/profile', 'Profile']]

  return <div className="app-layout">
    <a className="skip-link" href="#main-content" onClick={(event) => { event.preventDefault(); document.getElementById('main-content').focus() }}>Skip to content</a>
    <aside className="sidebar">
      <Brand />
      <nav aria-label="Main navigation">
        {links.map(([href, label]) => <a key={href} href={`#${href}`} aria-current={path === href ? 'page' : undefined}><Icon name={label.toLowerCase()} size={18} /><span>{label}</span></a>)}
      </nav>
      <div className="sidebar-bottom">
        <button className="logout" onClick={() => logout()}><Icon name="logout" size={18} />Logout</button>
      </div>
    </aside>
    <div className="workspace">
      <header className="topbar"><span className="header-title">{title}</span><a href="#/profile" className="header-account" aria-label="Open your profile"><span className="account-icon"><Icon name="profile" size={19} /></span><span className="account-details"><span className="account-name">{fullName}</span><span className="account-role">{role}</span></span></a></header>
      <main id="main-content" tabIndex={-1}>{children}</main>
    </div>
  </div>
}
