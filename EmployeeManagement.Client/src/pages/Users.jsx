import { useState } from 'react'
import { useAuth } from '../auth/useAuth'
import useApiList from '../hooks/useApiList'
import { ConfirmDialog, FormField, ListState, Modal, Notice } from '../components/ManagementUI'
import { personName, validateUser } from './managementValidation'

function UserEditor({ user, onClose, onSaved }) {
  const { request, session } = useAuth()
  const creating = !user
  const [form, setForm] = useState(() => ({ firstName: user?.firstName || '', lastName: user?.lastName || '', email: user?.email || '', role: user?.roles.includes('Administrator') ? 'Administrator' : 'Employee', password: '' }))
  const [errors, setErrors] = useState({})
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  function change(event) {
    const { name, value } = event.target
    setForm((current) => ({ ...current, [name]: value }))
    setErrors((current) => ({ ...current, [name]: '' }))
    setError('')
  }
  async function submit(event) {
    event.preventDefault()
    if (busy) return
    const nextErrors = validateUser(form, creating)
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length) return
    setBusy(true)
    setError('')
    try {
      const body = { firstName: form.firstName.trim(), lastName: form.lastName.trim(), email: form.email.trim(), role: form.role, ...(creating ? { password: form.password } : {}) }
      const result = await request(creating ? '/users' : `/users/${encodeURIComponent(user.id)}`, { method: creating ? 'POST' : 'PUT', body: JSON.stringify(body) })
      onSaved(result, creating)
      onClose()
    } catch (failure) { setError(failure.message) }
    finally { setBusy(false) }
  }
  return <Modal title={creating ? 'Create user' : 'Edit user'} onClose={onClose} busy={busy}>
    <p className="muted">{creating ? 'Create an account and assign its role. Only administrators can create users.' : 'Update account details and assign one role. Passwords are not changed here.'}</p>
    {user?.id === session.user.id && <p className="field-help">Changing your email or role will sign you out so you can log in again.</p>}
    {user?.roles.length > 1 && <p className="field-help">Saving replaces the current roles with the single role selected below.</p>}
    <form className="management-form" onSubmit={submit} noValidate>
      <fieldset disabled={busy}>
        <div className="management-fields">
          <FormField label="First name" name="firstName" value={form.firstName} onChange={change} required maxLength={100} autoComplete="given-name" error={errors.firstName} />
          <FormField label="Last name" name="lastName" value={form.lastName} onChange={change} required maxLength={100} autoComplete="family-name" error={errors.lastName} />
        </div>
        <FormField label="Email" name="email" type="email" value={form.email} onChange={change} required maxLength={256} autoComplete="off" error={errors.email} />
        <FormField label="Role" name="role" value={form.role} onChange={change} required error={errors.role}><option value="Employee">Employee</option><option value="Administrator">Administrator</option></FormField>
        {creating && <FormField label="Initial password" name="password" type="password" value={form.password} onChange={change} required autoComplete="new-password" minLength={6} error={errors.password} help="At least 6 characters, with uppercase, lowercase, a number and a symbol." />}
      </fieldset>
      <Notice error={error} />
      <div className="modal-actions"><button type="button" className="secondary" disabled={busy} onClick={onClose}>Cancel</button><button className="primary" disabled={busy}>{busy ? <><span className="spinner" />Saving…</> : creating ? 'Create user' : 'Save changes'}</button></div>
    </form>
  </Modal>
}

export default function Users() {
  const { session, request, updateProfile, logout } = useAuth()
  const administrator = session.user.roles.includes('Administrator')
  const list = useApiList(administrator ? '/users' : null)
  const [editor, setEditor] = useState(null)
  const [deleting, setDeleting] = useState(null)
  const [success, setSuccess] = useState('')

  function saved(user, creating) {
    if (user.id === session.user.id) {
      const rolesChanged = user.roles.length !== session.user.roles.length || user.roles.some((role) => !session.user.roles.includes(role))
      if (user.email !== session.user.email || rolesChanged) {
        logout('Your account was updated. Please sign in again to refresh your access.')
        return
      }
      updateProfile(user)
    }
    setSuccess(creating ? 'User created successfully.' : 'User updated successfully.')
    list.refresh()
  }

  if (!administrator) return <Notice error="Only Administrators can manage users." />
  return <>
    <div className="page-heading management-heading"><div><h1>Users</h1><p className="muted">Manage user accounts and their roles.</p></div><button className="primary" onClick={() => { setSuccess(''); setEditor({ user: null }) }}>Create user</button></div>
    <Notice success={success} />
    <section className="management-panel" aria-label="User accounts">
      <div className="list-toolbar"><h2>User accounts</h2><button className="text-button" disabled={list.loading} onClick={list.refresh}>Refresh</button></div>
      <ListState loading={list.loading} error={list.error} onRetry={list.refresh} empty={!list.data.length}>No users found.</ListState>
      {!list.loading && !list.error && list.data.length > 0 && <div className="table-scroll"><table className="management-table"><caption className="sr-only">All user accounts</caption><thead><tr><th scope="col">Name</th><th scope="col">Email</th><th scope="col">Role</th><th scope="col" className="actions-cell">Actions</th></tr></thead><tbody>
        {list.data.map((user) => <tr key={user.id}><th scope="row"><span className="table-name">{personName(user)}</span>{user.id === session.user.id && <span className="you-label">You</span>}</th><td>{user.email || 'Not available'}</td><td><div className="role-tags">{user.roles.map((role) => <span className="role-tag" key={role}>{role}</span>)}</div></td><td className="actions-cell"><div className="row-actions"><button className="text-button" aria-label={`Edit ${personName(user)}`} onClick={() => { setSuccess(''); setEditor({ user }) }}>Edit</button><button className="text-button destructive" disabled={user.id === session.user.id} title={user.id === session.user.id ? 'You cannot delete your own account.' : undefined} aria-label={`Delete ${personName(user)}`} onClick={() => { setSuccess(''); setDeleting(user) }}>Delete</button></div></td></tr>)}
      </tbody></table></div>}
    </section>
    {editor && <UserEditor user={editor.user} onClose={() => setEditor(null)} onSaved={saved} />}
    {deleting && <ConfirmDialog title="Delete user?" onClose={() => setDeleting(null)} onConfirm={async () => {
      await request(`/users/${encodeURIComponent(deleting.id)}`, { method: 'DELETE' })
      setSuccess('User deleted successfully.')
      list.refresh()
    }}><p>Delete <strong>{personName(deleting)}</strong>? This cannot be undone.</p><p>Project memberships will be removed and assigned tasks will become unassigned. Users who created tasks cannot be deleted.</p></ConfirmDialog>}
  </>
}
