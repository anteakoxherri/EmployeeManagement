import { useAuth } from '../auth/useAuth'
import useApiList from '../hooks/useApiList'
import Icon from '../components/Icon'
import { ListState } from '../components/ManagementUI'
import { createdDate, personName } from './managementValidation'

function Metric({ label, value, resource, icon, detail, href }) {
  const content = <>
    <div className="metric-top"><span>{label}</span><Icon name={icon} size={19} /></div>
    <strong className="metric-value">{resource.loading || resource.error ? '—' : value}</strong>
    <span className="metric-detail">{resource.loading ? 'Loading…' : resource.error ? 'Temporarily unavailable' : detail}</span>
  </>
  return href ? <a className="dashboard-metric" href={href} aria-busy={resource.loading}>{content}</a> : <div className="dashboard-metric" aria-busy={resource.loading}>{content}</div>
}

export default function Dashboard() {
  const { session } = useAuth()
  const administrator = session.user.roles.includes('Administrator')
  const projects = useApiList('/projects')
  const tasks = useApiList('/tasks')
  // Vetem Administratori kerkon listen e perdoruesve.
  const users = useApiList(administrator ? '/users' : null)
  const scopedTasks = administrator ? tasks.data : tasks.data.filter((task) => task.assignedToUserId === session.user.id)
  const completed = scopedTasks.filter((task) => task.isCompleted).length
  const openTasks = scopedTasks.filter((task) => !task.isCompleted)
  const percent = scopedTasks.length ? Math.round(completed / scopedTasks.length * 100) : 0
  const recentProjects = [...projects.data].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt) || b.id - a.id).slice(0, 4)
  const recentTasks = [...openTasks].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt) || b.id - a.id).slice(0, 4)
  const loading = projects.loading || tasks.loading || (administrator && users.loading)

  function refresh() {
    projects.refresh()
    tasks.refresh()
    if (administrator) users.refresh()
  }

  return <>
    <div className="page-heading management-heading"><div><h1>{administrator ? 'Administrator' : 'Employee'} Dashboard</h1><p className="muted">{administrator ? 'An overview of your people, projects and workload.' : 'Your projects and assigned work, all in one place.'}</p></div><button className="secondary dashboard-refresh" disabled={loading} onClick={refresh}>Refresh overview</button></div>
    <section className="welcome-banner dashboard-welcome" aria-label="Welcome">
      <div><h2>Welcome back</h2><p>{administrator ? 'See what is moving forward and where work is still open.' : 'See your assigned workload and stay connected to your projects.'}</p></div>
      <a href="#/projects" className="welcome-link">View projects<Icon name="arrow" size={17} /></a>
    </section>
    <section className="dashboard-metrics" aria-label="Workspace summary">
      <Metric label={administrator ? 'Total users' : 'Your projects'} value={administrator ? users.data.length : projects.data.length} resource={administrator ? users : projects} icon={administrator ? 'users' : 'projects'} detail={administrator ? 'Registered accounts' : 'Projects you belong to'} href={administrator ? '#/users' : '#/projects'} />
      <Metric label={administrator ? 'Projects' : 'Project tasks'} value={administrator ? projects.data.length : tasks.data.length} resource={administrator ? projects : tasks} icon="projects" detail={administrator ? 'Across your organization' : 'All tasks in your projects'} href={administrator ? '#/projects' : undefined} />
      <Metric label={administrator ? 'Open tasks' : 'Your open tasks'} value={openTasks.length} resource={tasks} icon="tasks" detail={administrator ? 'Not completed yet' : 'Assigned to you'} />
      <Metric label={administrator ? 'Completed tasks' : 'Your completed tasks'} value={completed} resource={tasks} icon="tasks" detail={administrator ? 'Across all projects' : 'Assigned to you'} />
    </section>
    <div className="dashboard-panels">
      <section className="dashboard-panel" aria-labelledby="recent-projects-title">
        <div className="dashboard-panel-heading"><div><h2 id="recent-projects-title">Recent projects</h2><p className="muted">{administrator ? 'The latest projects created.' : 'The latest projects you belong to.'}</p></div><a href="#/projects" className="text-button">View all<Icon name="arrow" size={15} /></a></div>
        <ListState loading={projects.loading} error={projects.error} onRetry={projects.refresh} empty={!projects.data.length}>{administrator ? 'No projects yet. Create your first project from the Projects page.' : 'You are not assigned to any projects yet.'}</ListState>
        {!projects.loading && !projects.error && recentProjects.length > 0 && <ul className="dashboard-project-list">
          {recentProjects.map((project) => <li key={project.id}><span className="dashboard-list-icon"><Icon name="projects" size={20} /></span><div><h3>{project.name}</h3><p>{project.description || 'No description provided.'}</p><span>Created {createdDate(project.createdAt)}</span></div></li>)}
        </ul>}
      </section>
      <section className="dashboard-panel" aria-labelledby="workload-title">
        <div className="dashboard-panel-heading"><div><h2 id="workload-title">{administrator ? 'Task overview' : 'Your task overview'}</h2><p className="muted">{administrator ? 'Current work across all projects.' : 'Only tasks assigned to you.'}</p></div><Icon name="tasks" size={20} /></div>
        <ListState loading={tasks.loading} error={tasks.error} onRetry={tasks.refresh} empty={false} />
        {!tasks.loading && !tasks.error && <div className="dashboard-workload">
          <div className="progress-label"><span>Completed</span><strong>{completed} of {scopedTasks.length} tasks</strong></div>
          <progress className="dashboard-progress" value={completed} max={scopedTasks.length || 1} aria-label={administrator ? 'Completed project tasks' : 'Your completed assigned tasks'} />
          <div className="progress-caption"><span>{percent}% complete</span><span>{openTasks.length} open</span></div>
          <h3 className="open-tasks-heading">{administrator ? 'Latest open tasks' : 'Your latest open tasks'}</h3>
          {recentTasks.length ? <ul className="dashboard-task-list">{recentTasks.map((task) => {
            const project = projects.data.find((item) => item.id === task.projectId)
            const assignee = administrator && users.data.find((item) => item.id === task.assignedToUserId)
            return <li key={task.id}><span className="task-open-marker" aria-hidden="true" /><div><h4>{task.title}</h4><p>{project?.name || `Project #${task.projectId}`}{administrator && <> · {task.assignedToUserId ? assignee ? personName(assignee) : 'Assigned' : 'Unassigned'}</>}</p></div><span className="task-state">Open</span></li>
          })}</ul> : <p className="dashboard-no-tasks">No open tasks to display.</p>}
          <p className="dashboard-readonly">Read-only overview. Manage tasks from the Tasks page.</p>
        </div>}
      </section>
    </div>
    <nav className="dashboard-shortcuts" aria-label="Dashboard shortcuts"><span>Quick access</span>{administrator && <a href="#/users"><Icon name="users" size={16} />Users</a>}<a href="#/projects"><Icon name="projects" size={16} />Projects</a><a href="#/profile"><Icon name="profile" size={16} />My profile</a></nav>
  </>
}
