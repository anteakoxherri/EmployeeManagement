import { useAuth } from '../auth/useAuth'
import Icon from '../components/Icon'

export default function Dashboard() {
  const { session } = useAuth()
  const administrator = session.user.roles.includes('Administrator')
  const cards = [
    ...(administrator ? [['Users', 'User accounts and roles.', '/users']] : []),
    ['Projects', administrator ? 'Projects and their memberships.' : 'Projects you belong to.', '/projects'],
    ['Tasks', administrator ? 'Tasks across projects.' : 'Your assigned tasks and project tasks.', '/tasks'],
    ['Profile', 'Your personal details and profile picture.', '/profile'],
  ]

  return <>
    <div className="page-heading"><h1>{administrator ? 'Administrator' : 'Employee'} Dashboard</h1><p className="muted">Your workspace, organized around you.</p></div>
    <section className="welcome-banner" aria-label="Welcome">
      <div><h2>Welcome back</h2><p>Pick up where you left off. Your projects and tasks are a click away.</p></div>
      <a href="#/projects" className="welcome-link">View projects<Icon name="arrow" size={17} /></a>
    </section>
    <div className="section-heading"><h2>Your workspace</h2><p className="muted">Choose where to continue.</p></div>
    <div className="card-grid">
      {cards.map(([title, description, href]) => <a className="section-card" key={title} href={`#${href}`}>
        <span className={`section-icon section-icon-${title.toLowerCase()}`}><Icon name={title.toLowerCase()} size={23} /></span>
        <div><h2>{title}</h2><p>{description}</p></div>
        <Icon name="arrow" size={17} />
      </a>)}
    </div>
  </>
}
