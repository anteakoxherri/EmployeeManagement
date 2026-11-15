import { useId, useState } from 'react'
import { useAuth } from '../auth/useAuth'
import useApiList from '../hooks/useApiList'
import { ConfirmDialog, FormField, ListState, Modal, Notice } from '../components/ManagementUI'
import { createdDate, personName, validateProject } from './managementValidation'

function ProjectEditor({ project, onClose, onSaved }) {
  const { request } = useAuth()
  const [form, setForm] = useState(() => ({ name: project?.name || '', description: project?.description || '' }))
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const descriptionId = useId()
  function change(event) {
    const { name, value } = event.target
    setForm((current) => ({ ...current, [name]: value }))
    setErrors((current) => ({ ...current, [name]: '' }))
    setError('')
  }
  async function submit(event) {
    event.preventDefault()
    if (busy) return
    const nextErrors = validateProject(form)
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) return
    setBusy(true)
    setError('')
    try {
      await request(project ? `/projects/${project.id}` : '/projects', { method: project ? 'PUT' : 'POST', body: JSON.stringify({ name: form.name.trim(), description: form.description.trim() || null }) })
      onSaved(project ? 'Project updated successfully.' : 'Project created successfully.')
      onClose()
    } catch (failure) { setError(failure.message) }
    finally { setBusy(false) }
  }
  return <Modal title={project ? 'Edit project' : 'Create project'} onClose={onClose} busy={busy}>
    <p className="muted">Set the project details. Manage its employees separately from the project list.</p>
    <form className="management-form" onSubmit={submit} noValidate>
      <fieldset disabled={busy}>
        <FormField label="Project name" name="name" value={form.name} onChange={change} maxLength={100} required error={errors.name} />
        <div className="field"><label htmlFor={descriptionId}>Description <span className="optional-label">Optional</span></label><textarea id={descriptionId} name="description" rows={4} maxLength={500} value={form.description} onChange={change} aria-invalid={Boolean(errors.description)} aria-describedby={`${descriptionId}-help`} /><p id={`${descriptionId}-help`} className={errors.description ? 'field-error' : 'field-help'}>{errors.description || `${form.description.length}/500 characters`}</p></div>
      </fieldset>
      <Notice error={error} />
      <div className="modal-actions"><button type="button" className="secondary" disabled={busy} onClick={onClose}>Cancel</button><button className="primary" disabled={busy}>{busy ? <><span className="spinner" />Saving…</> : project ? 'Save changes' : 'Create project'}</button></div>
    </form>
  </Modal>
}

function ProjectMembers({ project, administrator, onClose }) {
  const { request } = useAuth()
  const members = useApiList(`/projects/${project.id}/employees`)
  // Employee nuk kerkon listen administrative /employees.
  const employees = useApiList(administrator ? '/employees' : null)
  const [employeeId, setEmployeeId] = useState('')
  const [removing, setRemoving] = useState(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const available = employees.data.filter((employee) => !members.data.some((member) => member.id === employee.id))
  const unavailable = members.loading || Boolean(members.error) || employees.loading || Boolean(employees.error)

  async function add(event) {
    event.preventDefault()
    if (busy || unavailable || !employeeId) return
    setBusy(true)
    setError('')
    setSuccess('')
    try {
      await request(`/projects/${project.id}/employees/${encodeURIComponent(employeeId)}`, { method: 'POST' })
      setEmployeeId('')
      setSuccess('Employee added to the project.')
      members.refresh()
    } catch (failure) { setError(failure.message) }
    finally { setBusy(false) }
  }

  return <>
    <Modal title="Project details & members" onClose={onClose} busy={busy} wide>
      <div className="project-detail"><h3>{project.name}</h3><p>{project.description || 'No description provided.'}</p><span>Project #{project.id} · Created {createdDate(project.createdAt)}</span></div>
      <div className="list-toolbar modal-toolbar"><h3>Project employees</h3><button className="text-button" disabled={busy || members.loading || employees.loading} onClick={() => { members.refresh(); if (administrator) employees.refresh() }}>Refresh</button></div>
      <Notice success={success} />
      <ListState loading={members.loading} error={members.error} onRetry={members.refresh} empty={!members.data.length}>No employees have been assigned to this project.</ListState>
      {!members.loading && !members.error && members.data.length > 0 && <div className="table-scroll"><table className="management-table"><caption className="sr-only">Employees in {project.name}</caption><thead><tr><th scope="col">Employee</th><th scope="col">Email</th>{administrator && <th scope="col" className="actions-cell">Action</th>}</tr></thead><tbody>{members.data.map((member) => <tr key={member.id}><th scope="row">{personName(member)}</th><td>{member.email || 'Not available'}</td>{administrator && <td className="actions-cell"><button className="text-button destructive" disabled={busy} aria-label={`Remove ${personName(member)} from project`} onClick={() => { setSuccess(''); setError(''); setRemoving(member) }}>Remove</button></td>}</tr>)}</tbody></table></div>}
      {administrator && <form className="management-form membership-form" onSubmit={add}>
        <h3>Add an employee</h3>
        <p className="field-help">Only users with the Employee role can be added. Existing members are excluded.</p>
        {employees.error && <><Notice error={employees.error} /><button type="button" className="text-button" disabled={employees.loading} onClick={employees.refresh}>Reload employee options</button></>}
        <div className="membership-controls"><FormField label="Employee" name="employeeId" value={employeeId} onChange={(event) => { setEmployeeId(event.target.value); setError('') }} required disabled={busy || unavailable || !available.length}>
          <option value="">{employees.loading ? 'Loading employees…' : !available.length ? 'No additional employees available' : 'Select an employee'}</option>
          {available.map((employee) => <option key={employee.id} value={employee.id}>{employee.email || employee.userName || employee.id}</option>)}
        </FormField><button className="primary" disabled={busy || unavailable || !employeeId}>{busy ? <><span className="spinner" />Working…</> : 'Add employee'}</button></div>
        <Notice error={error} />
      </form>}
      {!administrator && <p className="field-help">Project details and memberships are read-only. Contact your administrator for changes.</p>}
      <div className="modal-actions"><button className="secondary" disabled={busy} onClick={onClose}>Close</button></div>
    </Modal>
    {removing && <ConfirmDialog title="Remove project member?" confirmLabel="Remove employee" onClose={() => setRemoving(null)} onConfirm={async () => {
      setBusy(true)
      try {
        await request(`/projects/${project.id}/employees/${encodeURIComponent(removing.id)}`, { method: 'DELETE' })
        setSuccess('Employee removed from the project.')
        members.refresh()
      } finally { setBusy(false) }
    }}><p>Remove <strong>{personName(removing)}</strong> from <strong>{project.name}</strong>?</p><p>The user account will remain. Removal is blocked if the employee still has assigned tasks in this project.</p></ConfirmDialog>}
  </>
}

export default function Projects() {
  const { session, request } = useAuth()
  const administrator = session.user.roles.includes('Administrator')
  const list = useApiList('/projects')
  const [editor, setEditor] = useState(null)
  const [deleting, setDeleting] = useState(null)
  const [selected, setSelected] = useState(null)
  const [success, setSuccess] = useState('')
  function saved(message) { setSuccess(message); list.refresh() }
  return <>
    <div className="page-heading management-heading"><div><h1>Projects</h1><p className="muted">{administrator ? 'Manage projects and the employees working on them.' : 'View your projects and the colleagues working with you.'}</p></div>{administrator && <button className="primary" onClick={() => { setSuccess(''); setEditor({ project: null }) }}>Create project</button>}</div>
    <Notice success={success} />
    <section className="management-panel" aria-label="Projects">
      <div className="list-toolbar"><h2>{administrator ? 'All projects' : 'Your projects'}</h2><button className="text-button" disabled={list.loading} onClick={list.refresh}>Refresh</button></div>
      <ListState loading={list.loading} error={list.error} onRetry={list.refresh} empty={!list.data.length}>{administrator ? 'No projects yet. Create a project to get started.' : 'You are not assigned to any projects yet.'}</ListState>
      {!list.loading && !list.error && list.data.length > 0 && <div className="table-scroll"><table className="management-table projects-table"><caption className="sr-only">Accessible projects</caption><thead><tr><th scope="col">Project</th><th scope="col">Description</th><th scope="col">Created</th><th scope="col" className="actions-cell">Actions</th></tr></thead><tbody>{list.data.map((project) => <tr key={project.id}><th scope="row"><span className="table-name">{project.name}</span><span className="table-meta">Project #{project.id}</span></th><td><p className="project-description">{project.description || 'No description provided.'}</p></td><td className="date-cell">{createdDate(project.createdAt)}</td><td className="actions-cell"><div className="row-actions"><button className="text-button" aria-label={`View ${project.name} and its members`} onClick={() => setSelected(project)}>Details & members</button>{administrator && <><button className="text-button" aria-label={`Edit ${project.name}`} onClick={() => { setSuccess(''); setEditor({ project }) }}>Edit</button><button className="text-button destructive" aria-label={`Delete ${project.name}`} onClick={() => { setSuccess(''); setDeleting(project) }}>Delete</button></>}</div></td></tr>)}</tbody></table></div>}
    </section>
    {administrator && editor && <ProjectEditor project={editor.project} onClose={() => setEditor(null)} onSaved={saved} />}
    {selected && <ProjectMembers key={selected.id} project={selected} administrator={administrator} onClose={() => setSelected(null)} />}
    {administrator && deleting && <ConfirmDialog title="Delete project?" onClose={() => setDeleting(null)} onConfirm={async () => {
      // API kontrollon detyrat e hapura ne transaksion, jo nje numerim ne frontend.
      await request(`/projects/${deleting.id}`, { method: 'DELETE' })
      saved('Project deleted successfully.')
    }}><p>Delete <strong>{deleting.name}</strong>? This cannot be undone.</p><p>Projects with open tasks cannot be deleted. If deletion is allowed, completed tasks and memberships in this project will also be removed.</p></ConfirmDialog>}
  </>
}
