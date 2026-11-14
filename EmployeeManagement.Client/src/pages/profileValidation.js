export const MAX_PICTURE_SIZE = 2 * 1024 * 1024
export const MAX_PICTURE_PIXELS = 16_000_000

// Pasqyrojme PhoneAttribute te .NET 8; API mbetet kontrolli perfundimtar.
export function validPhone(value) {
  let phone = value.replaceAll('+', '').trimEnd()
  for (const suffix of ['ext.', 'ext', 'x']) {
    const index = phone.toLowerCase().lastIndexOf(suffix)
    if (index >= 0 && /^\p{Nd}+$/u.test(phone.slice(index + suffix.length).trimStart())) {
      phone = phone.slice(0, index)
      break
    }
  }
  return /\p{Nd}/u.test(phone) && /^[\p{Nd}\s.()-]+$/u.test(phone)
}

export function validateProfile(form) {
  const errors = {}
  for (const [key, label] of [['firstName', 'First name'], ['lastName', 'Last name']]) {
    if (!form[key].trim()) errors[key] = `${label} is required.`
    else if (form[key].length > 100) errors[key] = `${label} must be at most 100 characters.`
  }
  if (form.phoneNumber.length > 30) errors.phoneNumber = 'Phone number must be at most 30 characters.'
  else if (form.phoneNumber.trim() && !validPhone(form.phoneNumber)) errors.phoneNumber = 'Enter a valid phone number, for example +355 69 123 4567.'
  return errors
}

export function validatePictureFile(file) {
  if (!file) return 'Choose a picture first.'
  if (!/\.(jpe?g|png)$/i.test(file.name)) return 'Only JPG, JPEG and PNG images are allowed.'
  if (!file.size || file.size > MAX_PICTURE_SIZE) return 'The image must contain data and be no larger than 2 MB.'
  return ''
}

export function matchesPictureSignature(fileName, bytes) {
  if (/\.png$/i.test(fileName)) {
    return [137, 80, 78, 71, 13, 10, 26, 10].every((value, index) => bytes[index] === value)
  }
  return bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255
}

export async function validatePicture(file) {
  const error = validatePictureFile(file)
  if (error) return error
  const bytes = new Uint8Array(await file.slice(0, 12).arrayBuffer())
  if (!matchesPictureSignature(file.name, bytes)) return 'The image content must match its JPG, JPEG or PNG extension.'
  const url = URL.createObjectURL(file)
  try {
    const image = new Image()
    image.src = url
    await image.decode()
    if (image.naturalWidth <= 0 || image.naturalHeight <= 0) return 'Choose a valid JPG, JPEG or PNG image.'
    if (image.naturalWidth * image.naturalHeight > MAX_PICTURE_PIXELS) return 'The image must contain at most 16 million pixels.'
    return ''
  } catch {
    return 'The file is not a valid JPG, JPEG or PNG image.'
  } finally {
    URL.revokeObjectURL(url)
  }
}

export function profileForm(profile) {
  return { firstName: profile.firstName || '', lastName: profile.lastName || '', phoneNumber: profile.phoneNumber || '' }
}

export function profilePayload(form) {
  return { firstName: form.firstName.trim(), lastName: form.lastName.trim(), phoneNumber: form.phoneNumber.trim() || null }
}
