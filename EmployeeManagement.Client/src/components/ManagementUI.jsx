import { useEffect, useId, useRef, useState } from 'react'
import Icon from './Icon'

export function Notice({ error, success }) {
  if (error) return <p className="error" role="alert"><Icon name="info" size={18} /><span>{error}</span></p>
  if (success) return <p className="profile-success" role="status">{success}</p>
  return null
}

export function ListState({ loading, error, onRetry, empty, children }) {
  if (loading) return <div className="management-state" role="status"><span className="spinner" />Loading…</div>
  if (error) return <div className="management-state"><Notice error={error} /><button className="secondary" onClick={onRetry}>Try again</button></div>
  if (empty) return <div className="management-state"><Icon name="info" size={24} /><p>{children}</p></div>
  return null
}

export function Modal({ title, children, onClose, busy = false, wide = false }) {
  const ref = useRef(null)
  const titleId = useId()
  useEffect(() => {
    const dialog = ref.current
    const previous = document.activeElement
    // Dialog-u vendas menaxhon fokusin, tastieren dhe bllokimin e sfondit.
    dialog.showModal()
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      dialog.close()
      document.body.style.overflow = overflow
      if (previous instanceof HTMLElement && previous.isConnected) previous.focus()
    }
  }, [])

  return <dialog ref={ref} className={`management-modal${wide ? ' modal-wide' : ''}`} aria-labelledby={titleId} aria-busy={busy} onCancel={(event) => { event.preventDefault(); if (!busy) onClose() }}>
    <div className="modal-header"><h2 id={titleId}>{title}</h2><button type="button" className="modal-close" aria-label="Close dialog" disabled={busy} onClick={onClose}>×</button></div>
    <div className="modal-body">{children}</div>
  </dialog>
}

export function ConfirmDialog({ title, children, onConfirm, onClose, confirmLabel = 'Delete' }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function confirm() {
    if (busy) return
    setBusy(true)
    setError('')
    try { await onConfirm(); onClose() }
    catch (failure) { setError(failure.message) }
    finally { setBusy(false) }
  }
  return <Modal title={title} onClose={onClose} busy={busy}>
    <div className="confirmation-copy">{children}</div>
    <Notice error={error} />
    <div className="modal-actions"><button type="button" className="secondary" disabled={busy} onClick={onClose} autoFocus>Cancel</button><button type="button" className="danger-button" disabled={busy} onClick={confirm}>{busy ? <><span className="spinner" />Working…</> : confirmLabel}</button></div>
  </Modal>
}

export function FormField({ label, name, value, onChange, error, help, children, ...props }) {
  const id = useId()
  return <div className="field">
    <label htmlFor={id}>{label}</label>
    {children ? <select id={id} name={name} value={value} onChange={onChange} aria-invalid={Boolean(error)} aria-describedby={error || help ? `${id}-help` : undefined} {...props}>{children}</select> : <input id={id} name={name} value={value} onChange={onChange} aria-invalid={Boolean(error)} aria-describedby={error || help ? `${id}-help` : undefined} {...props} />}
    {(error || help) && <p id={`${id}-help`} className={error ? 'field-error' : 'field-help'}>{error || help}</p>}
  </div>
}
