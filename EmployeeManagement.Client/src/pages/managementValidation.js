export function validateUser(form, creating) {
  const errors = {}
  for (const [key, label] of [['firstName', 'First name'], ['lastName', 'Last name']]) {
    if (!form[key].trim()) errors[key] = `${label} is required.`
    else if (form[key].length > 100) errors[key] = `${label} must be at most 100 characters.`
  }
  if (!form.email.trim() || !/^[^\s@]+@[^\s@]+$/.test(form.email.trim())) errors.email = 'Enter a valid email address.'
  else if (form.email.length > 256) errors.email = 'Email must be at most 256 characters.'
  if (!['Administrator', 'Employee'].includes(form.role)) errors.role = 'Select Administrator or Employee.'
  // Identity perdor rregullat standard te password-it te konfiguruara ne server.
  if (creating && (form.password.length < 6 || !/[a-z]/.test(form.password) || !/[A-Z]/.test(form.password) || !/[0-9]/.test(form.password) || !/[^a-zA-Z0-9]/.test(form.password))) {
    errors.password = 'Use at least 6 characters, including uppercase, lowercase, a number and a symbol.'
  }
  return errors
}

export function validateProject(form) {
  const errors = {}
  if (!form.name.trim()) errors.name = 'Project name is required.'
  else if (form.name.length > 100) errors.name = 'Project name must be at most 100 characters.'
  if (form.description.length > 500) errors.description = 'Description must be at most 500 characters.'
  return errors
}

export function personName(user) {
  return [user.firstName, user.lastName].filter(Boolean).join(' ') || user.email || user.userName || 'User'
}

export function createdDate(value) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 'Not available' : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(date)
}
