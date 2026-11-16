import { useEffect, useId, useState } from 'react'
import { useAuth } from '../auth/useAuth'
import useApiList from '../hooks/useApiList'
import useTaskMembers from '../hooks/useTaskMembers'
import Icon from '../components/Icon'
import { ConfirmDialog, FormField, ListState, Modal, Notice } from '../components/ManagementUI'
import { personName } from './managementValidation'
import './Tasks.css'

function TaskEditor({ task, initialProject, projects, employees, onClose, onSaved }) {
  const { session, request } = useAuth()
  const administrator = session.user.roles.includes('Administrator')
  const employeeEdit = Boolean(task) && !administrator
  const [form, setForm] = useState(() => ({
    title: task?.title || '', description: task?.description || '',
    projectId: String(task?.projectId || initialProject || ''), assignedToUserId: task?.assignedToUserId || '',
  }))
  const [members, setMembers] = useState({ projectId: '', data: [], loading: false, error: '' })
  const [revision, setRevision] = useState(0)
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const descriptionId = useId()

  useEffect(() => {
    if (!form.projectId || employeeEdit) return
    const controller = new AbortController()
    const projectId = form.projectId
    request(`/projects/${projectId}/employees`, { signal: controller.signal, cache: 'no-store' })
      .then((data) => { if (!controller.signal.aborted) setMembers({ projectId, data, loading: false, error: '' }) })
      .catch((failure) => { if (!controller.signal.aborted) setMembers({ projectId, data: [], loading: false, error: failure.message }) })
    return () => controller.abort()
  }, [form.projectId, employeeEdit, request, revision])

  // Both project membership and the current Employee role are required by the API.
  const ready = employeeEdit || (Boolean(form.projectId) && members.projectId === form.projectId && !members.loading && !members.error)
  const eligible = ready ? members.data.filter((member) => employees.some((employee) => employee.id === member.id)) : []
  const invalidAssignee = !employeeEdit && form.assignedToUserId && ready && !eligible.some((member) => member.id === form.assignedToUserId)

  function change(event) {
    const { name, value } = event.target
    if (employeeEdit && !['title', 'description'].includes(name)) return
    if (name === 'projectId') setMembers({ projectId: '', data: [], loading: true, error: '' })
    setForm((current) => ({ ...current, [name]: value, ...(name === 'projectId' ? { assignedToUserId: '' } : {}) }))
    setErrors((current) => ({ ...current, [name]: '', ...(name === 'projectId' ? { assignedToUserId: '' } : {}) }))
    setError('')
  }

  async function submit(event) {
    event.preventDefault()
    if (busy || (task && !administrator && task.assignedToUserId !== session.user.id)) return
    const nextErrors = {}
    if (!form.title.trim()) nextErrors.title = 'Task title is required.'
    else if (form.title.length > 100) nextErrors.title = 'Title must be at most 100 characters.'
    if (form.description.length > 500) nextErrors.description = 'Description must be at most 500 characters.'
    if (!projects.some((project) => String(project.id) === form.projectId)) nextErrors.projectId = 'Select an available project.'
    if (invalidAssignee) nextErrors.assignedToUserId = 'Select an Employee who belongs to this project, or leave the task unassigned.'
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length || !ready) return
    setBusy(true)
    setError('')
    try {
      await request(task ? `/tasks/${task.id}` : '/tasks', {
        method: task ? 'PUT' : 'POST',
        body: JSON.stringify({ title: form.title.trim(), description: form.description.trim() || null,
          projectId: employeeEdit ? task.projectId : Number(form.projectId),
          assignedToUserId: employeeEdit ? task.assignedToUserId : form.assignedToUserId || null }),
      })
      // PUT omits IsCompleted so editing details preserves the server's current status.
      onSaved(task ? 'Task updated successfully.' : 'Task created successfully.')
      onClose()
    } catch (failure) { setError(failure.message) }
    finally { setBusy(false) }
  }

  return <Modal title={task ? 'Edit task' : 'Create task'} onClose={onClose} busy={busy}>
    <p className="muted">{employeeEdit ? 'Update the title and description. Project and assignment are managed by your administrator.' : 'Choose a project, add the details and assign an employee from its team.'}</p>
    <form className="management-form task-editor-form" onSubmit={submit} noValidate>
      <fieldset disabled={busy}>
        {employeeEdit ? <FormField label="Project" name="project" value={projects.find((project) => project.id === task.projectId)?.name || `Project #${task.projectId}`} readOnly /> : <FormField label="Project" name="projectId" value={form.projectId} onChange={change} required error={errors.projectId}>
          <option value="">Select a project</option>
          {form.projectId && !projects.some((project) => String(project.id) === form.projectId) && <option value={form.projectId}>Project unavailable — select another</option>}
          {projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
        </FormField>}
        <FormField label="Task title" name="title" value={form.title} onChange={change} maxLength={100} required error={errors.title} />
        <div className="field"><label htmlFor={descriptionId}>Description <span className="optional-label">Optional</span></label><textarea id={descriptionId} name="description" rows={4} maxLength={500} value={form.description} onChange={change} aria-invalid={Boolean(errors.description)} aria-describedby={`${descriptionId}-help`} /><p id={`${descriptionId}-help`} className={errors.description ? 'field-error' : 'field-help'}>{errors.description || `${form.description.length}/500 characters`}</p></div>
        {employeeEdit ? <FormField label="Assigned employee" name="assignedEmployee" value={personName(employees.find((employee) => employee.id === task.assignedToUserId) || session.user)} readOnly /> : <FormField label="Assigned employee (optional)" name="assignedToUserId" value={form.assignedToUserId} onChange={change} disabled={!ready} error={errors.assignedToUserId || (invalidAssignee ? 'The previous assignee is no longer eligible. Choose another employee or Unassigned.' : '')} help={!form.projectId ? 'Select a project to see its employees.' : !ready ? 'Loading project employees must finish before saving.' : !eligible.length ? 'This project has no eligible employees. You can save the task unassigned.' : 'Only Employees belonging to the selected project are listed.'}>
          <option value="">{form.projectId && !ready ? 'Employees unavailable or loading…' : 'Unassigned'}</option>
          {form.assignedToUserId && !eligible.some((member) => member.id === form.assignedToUserId) && <option value={form.assignedToUserId}>Current assignee — awaiting validation</option>}
          {eligible.map((member) => <option key={member.id} value={member.id}>{personName(member)}{member.email ? ` (${member.email})` : ''}</option>)}
        </FormField>}
        {!employeeEdit && members.projectId === form.projectId && members.error && <><Notice error={members.error} /><button type="button" className="text-button" onClick={() => { setMembers({ projectId: '', data: [], loading: true, error: '' }); setRevision((value) => value + 1) }}>Reload project employees</button></>}
      </fieldset>
      <Notice error={error} />
      <div className="modal-actions"><button type="button" className="secondary" disabled={busy} onClick={onClose}>Cancel</button><button className="primary" disabled={busy || (Boolean(form.projectId) && !ready)}>{busy ? <><span className="spinner" />Saving…</> : task ? 'Save changes' : 'Create task'}</button></div>
    </form>
  </Modal>
}

export default function Tasks({ projectFilter }) {
  const { session, request } = useAuth()
  const administrator = session.user.roles.includes('Administrator')
  const userId = session.user.id
  const tasks = useApiList('/tasks')
  const projects = useApiList('/projects')
  const employeeList = useApiList(administrator ? '/employees' : null)
  const projectMembers = useTaskMembers(projects.data, !administrator && !projects.loading && !projects.error)
  const employees = administrator ? employeeList : projectMembers
  const [status, setStatus] = useState('all')
  const [editor, setEditor] = useState(null)
  const [deleting, setDeleting] = useState(null)
  const [completing, setCompleting] = useState(null)
  const [success, setSuccess] = useState('')
  const [error, setError] = useState('')
  const loading = tasks.loading || projects.loading || employees.loading
  const loadError = tasks.error || projects.error || employees.error
  // The server filters membership; also hide stale tasks when project access changes.
  const accessible = administrator ? tasks.data : tasks.data.filter((task) => projects.data.some((project) => project.id === task.projectId))
  const visible = accessible.filter((task) => (!projectFilter || String(task.projectId) === projectFilter) && (status === 'all' || task.isCompleted === (status === 'completed')))
  const scoped = accessible.filter((task) => !projectFilter || String(task.projectId) === projectFilter)
  const completed = scoped.filter((task) => task.isCompleted).length
  const selectedProject = projects.data.find((project) => String(project.id) === projectFilter)
  function refresh() { tasks.refresh(); projects.refresh(); employees.refresh() }
  function saved(message) { setError(''); setSuccess(message); tasks.refresh() }
  const canModify = (task) => administrator || task.assignedToUserId === userId
  function openEditor(task = null) {
    if (task && !canModify(task)) return
    setSuccess(''); setError(''); setEditor({ task })
  }

  async function complete(task) {
    if (completing !== null || !canModify(task)) return
    setCompleting(task.id)
    setSuccess('')
    setError('')
    try {
      await request(`/tasks/${task.id}/complete`, { method: 'PATCH' })
      saved('Task marked as completed.')
    } catch (failure) { setError(failure.message) }
    finally { setCompleting(null) }
  }

  return <>
    <div className="page-heading management-heading"><div><h1>Tasks</h1><p className="muted">{administrator ? 'Organize project work, assign your team and track completion.' : 'View your project tasks, create work for your team and manage tasks assigned to you.'}</p></div><button className="primary" disabled={loading || Boolean(loadError) || !projects.data.length || completing !== null} onClick={() => openEditor()}><Icon name="tasks" size={18} />Create task</button></div>
    <Notice error={error} success={success} />
    {!loading && !loadError && <div className="task-summary" aria-label="Task overview"><div><span>Total tasks</span><strong>{scoped.length}</strong></div><div><span>Open</span><strong>{scoped.length - completed}</strong></div><div><span>Completed</span><strong>{completed}</strong></div></div>}
    <section className="management-panel" aria-label="Task management">
      <div className="list-toolbar"><h2>{selectedProject ? selectedProject.name : projectFilter ? `Project #${projectFilter}` : 'All tasks'}</h2><button className="text-button" disabled={loading || completing !== null} onClick={refresh}>Refresh</button></div>
      <div className="management-form task-filters">
        <FormField label="Filter by project" name="projectFilter" value={projectFilter} disabled={projects.loading} onChange={(event) => { window.location.hash = event.target.value ? `/tasks?projectId=${encodeURIComponent(event.target.value)}` : '/tasks' }}>
          <option value="">All projects</option>
          {projectFilter && !selectedProject && <option value={projectFilter}>Project #{projectFilter} (unavailable)</option>}
          {projects.data.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
        </FormField>
        <FormField label="Completion status" name="status" value={status} onChange={(event) => setStatus(event.target.value)}><option value="all">All statuses</option><option value="open">Open</option><option value="completed">Completed</option></FormField>
        <p className="field-help" role="status">{!loading && !loadError ? `${visible.length} task${visible.length === 1 ? '' : 's'} shown` : 'Loading task information…'}</p>
      </div>
      <ListState loading={loading} error={loadError} onRetry={refresh} empty={!visible.length}>{!projects.data.length ? (administrator ? 'Create a project in Projects before adding tasks.' : 'You are not a member of any projects yet.') : projectFilter || status !== 'all' ? 'No tasks match these filters. Choose another project or status.' : 'No tasks yet. Create a task to get started.'}</ListState>
      {!loading && !loadError && visible.length > 0 && <div className="task-grid">{visible.map((task) => {
        const project = projects.data.find((item) => item.id === task.projectId)
        const employee = employees.data.find((item) => item.id === task.assignedToUserId)
        const mine = task.assignedToUserId === userId
        return <article className={`task-card${!administrator && mine ? ' task-card-mine' : ''}`} key={task.id} aria-labelledby={`task-${task.id}`}>
          <div className="task-card-top"><span className="task-project"><Icon name="projects" size={15} />{project?.name || `Project #${task.projectId}`}</span><span className={`task-status ${task.isCompleted ? 'is-completed' : 'is-open'}`}>{task.isCompleted ? 'Completed' : 'Open'}</span></div>
          <h3 id={`task-${task.id}`}>{task.title}</h3><p className="task-description">{task.description || 'No description provided.'}</p>
          <div className="task-assignee"><Icon name="profile" size={17} /><span><small>{!administrator && mine ? 'Assigned to you' : 'Assigned employee'}</small>{employee ? personName(employee) : task.assignedToUserId ? `Employee unavailable (${task.assignedToUserId})` : 'Unassigned'}</span></div>
          <div className="task-actions">{canModify(task) && <button className="text-button" disabled={completing !== null} onClick={() => openEditor(task)} aria-label={`Edit ${task.title}`}>Edit</button>}{administrator && <button className="text-button destructive" disabled={completing !== null} onClick={() => { setSuccess(''); setError(''); setDeleting(task) }} aria-label={`Delete ${task.title}`}>Delete</button>}{!canModify(task) && <span className="task-readonly">Read-only: {task.assignedToUserId ? 'Assigned to another employee' : 'Unassigned task'}</span>}{canModify(task) && !task.isCompleted && <button className="text-button task-complete" disabled={completing !== null} onClick={() => complete(task)} aria-label={`Mark ${task.title} as completed`}>{completing === task.id ? 'Completing…' : 'Mark completed'}</button>}</div>
        </article>
      })}</div>}
    </section>
    {editor && (!editor.task || canModify(editor.task)) && <TaskEditor task={editor.task} initialProject={selectedProject?.id} projects={projects.data} employees={employees.data} onClose={() => setEditor(null)} onSaved={saved} />}
    {administrator && deleting && <ConfirmDialog title="Delete task?" onClose={() => setDeleting(null)} onConfirm={async () => { await request(`/tasks/${deleting.id}`, { method: 'DELETE' }); saved('Task deleted successfully.') }}><p>Delete <strong>{deleting.title}</strong>? This cannot be undone.</p><p>The task will be removed from its project.</p></ConfirmDialog>}
  </>
}
