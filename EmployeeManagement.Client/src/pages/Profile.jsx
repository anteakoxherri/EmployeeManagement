import { useEffect, useRef, useState } from 'react'
import { useAuth } from '../auth/useAuth'
import Icon from '../components/Icon'
import { profileForm, profilePayload, validateProfile, validatePicture, validatePictureFile } from './profileValidation'

function Feedback({ error, success }) {
  if (error) return <p className="error" role="alert"><Icon name="info" size={18} /><span>{error}</span></p>
  if (success) return <p className="profile-success" role="status">{success}</p>
  return null
}

function Picture({ path }) {
  const [failed, setFailed] = useState(false)
  return path && !failed ? <img className="profile-picture" src={path} alt="Your profile" onError={() => setFailed(true)} /> :
    <div className="profile-picture profile-picture-empty" role="img" aria-label="No profile picture"><Icon name="profile" size={44} /></div>
}

export default function Profile() {
  const { request, updateProfile } = useAuth()
  const [profile, setProfile] = useState(null)
  const [form, setForm] = useState({ firstName: '', lastName: '', phoneNumber: '' })
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [reload, setReload] = useState(0)
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [errors, setErrors] = useState({})
  const [saveError, setSaveError] = useState('')
  const [saveSuccess, setSaveSuccess] = useState('')
  const [file, setFile] = useState(null)
  const [pictureError, setPictureError] = useState('')
  const [pictureSuccess, setPictureSuccess] = useState('')
  const fileInput = useRef(null)
  const busy = saving || uploading

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setLoadError('')
      try {
        const result = await request('/profile/me', { signal: controller.signal })
        if (controller.signal.aborted) return
        setProfile(result)
        setForm(profileForm(result))
        updateProfile(result)
      } catch (error) {
        if (!controller.signal.aborted) setLoadError(error.message)
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    load()
    return () => controller.abort()
  }, [request, updateProfile, reload])

  function change(event) {
    const { name, value } = event.target
    setForm((current) => ({ ...current, [name]: value }))
    setErrors((current) => ({ ...current, [name]: '' }))
    setSaveError('')
    setSaveSuccess('')
  }

  async function save(event) {
    event.preventDefault()
    if (busy) return
    const nextErrors = validateProfile(form)
    setErrors(nextErrors)
    setSaveError('')
    setSaveSuccess('')
    if (Object.keys(nextErrors).length) return
    setSaving(true)
    try {
      // Dergojme vetem tri fushat qe lejon UpdateProfileDto.
      const result = await request('/profile/me', { method: 'PUT', body: JSON.stringify(profilePayload(form)) })
      setProfile(result)
      setForm(profileForm(result))
      updateProfile(result)
      setSaveSuccess('Your profile has been updated.')
    } catch (error) {
      setSaveError(error.message)
    } finally {
      setSaving(false)
    }
  }

  async function upload(event) {
    event.preventDefault()
    if (busy) return
    setPictureError('')
    setPictureSuccess('')
    setUploading(true)
    try {
      const error = await validatePicture(file)
      if (error) { setPictureError(error); return }
      const body = new FormData()
      body.append('File', file)
      const result = await request('/profile/me/picture', { method: 'POST', body })
      // Ndryshimi i fotos nuk mbishkruan tekstin ende te paruajtur ne formular.
      setProfile(result)
      updateProfile(result)
      setFile(null)
      if (fileInput.current) fileInput.current.value = ''
      setPictureSuccess('Your profile picture has been updated.')
    } catch (error) {
      setPictureError(error.message)
    } finally {
      setUploading(false)
    }
  }

  return <>
    <div className="page-heading"><h1>Profile</h1><p className="muted">Manage your personal details and profile picture.</p></div>
    {loading ? <div className="profile-panel profile-loading" role="status"><span className="spinner" />Loading your profile…</div> :
      loadError ? <section className="profile-panel"><Feedback error={loadError} /><button className="secondary" onClick={() => setReload((value) => value + 1)}>Try again</button></section> : profile &&
      <div className="profile-grid">
        <section className="profile-panel profile-photo-panel" aria-labelledby="picture-title">
          <h2 id="picture-title">Profile picture</h2>
          <p className="muted">Add a photo to personalize your account.</p>
          <Picture key={profile.profilePicturePath || 'empty'} path={profile.profilePicturePath} />
          <form onSubmit={upload} aria-busy={uploading}>
            <label htmlFor="profile-picture-file">Choose a new picture</label>
            <input ref={fileInput} id="profile-picture-file" type="file" accept=".jpg,.jpeg,.png,image/jpeg,image/png" disabled={busy} aria-describedby="picture-help" onChange={(event) => {
              const selected = event.target.files?.[0] || null
              setFile(selected)
              setPictureSuccess('')
              setPictureError(selected ? validatePictureFile(selected) : '')
            }} />
            <p id="picture-help" className="field-help">JPG, JPEG or PNG. Maximum 2 MB and 16 million pixels.</p>
            <button className="secondary" disabled={busy || !file || Boolean(validatePictureFile(file))}>{uploading ? <><span className="spinner" />Uploading…</> : 'Update picture'}</button>
            <Feedback error={pictureError} success={pictureSuccess} />
          </form>
        </section>
        <section className="profile-panel" aria-labelledby="details-title">
          <h2 id="details-title">Personal information</h2>
          <p className="muted">Keep your contact information up to date.</p>
          <form className="profile-form" onSubmit={save} noValidate aria-busy={saving}>
            <div className="profile-fields">
              {[['firstName', 'First name', 'given-name'], ['lastName', 'Last name', 'family-name'], ['phoneNumber', 'Phone number', 'tel']].map(([name, label, autoComplete]) => <div className={name === 'phoneNumber' ? 'field field-wide' : 'field'} key={name}>
                <label htmlFor={`profile-${name}`}>{label}{name === 'phoneNumber' && <span className="optional-label">Optional</span>}</label>
                <input id={`profile-${name}`} name={name} type={name === 'phoneNumber' ? 'tel' : 'text'} required={name !== 'phoneNumber'} maxLength={name === 'phoneNumber' ? 30 : 100} autoComplete={autoComplete} value={form[name]} onChange={change} disabled={busy} aria-invalid={Boolean(errors[name])} aria-describedby={errors[name] ? `${name}-error` : undefined} />
                {errors[name] && <p className="field-error" id={`${name}-error`}>{errors[name]}</p>}
              </div>)}
              <div className="field"><label htmlFor="profile-email">Email</label><input id="profile-email" value={profile.email || ''} readOnly aria-describedby="account-help" /></div>
              <div className="field"><label htmlFor="profile-role">Role</label><input id="profile-role" value={profile.role || ''} readOnly aria-describedby="account-help" /></div>
            </div>
            <p id="account-help" className="field-help">Email and role are managed by your administrator.</p>
            <Feedback error={saveError} success={saveSuccess} />
            <div className="profile-actions"><button className="primary" disabled={busy}>{saving ? <><span className="spinner" />Saving…</> : 'Save changes'}</button></div>
          </form>
        </section>
      </div>}
  </>
}
