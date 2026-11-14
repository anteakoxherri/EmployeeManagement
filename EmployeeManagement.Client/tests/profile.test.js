import assert from 'node:assert/strict'
import { test } from 'node:test'
import { apiRequest } from '../src/api/client.js'
import { MAX_PICTURE_SIZE, validateProfile, validPhone, profilePayload, validatePictureFile, matchesPictureSignature, validatePicture } from '../src/pages/profileValidation.js'

test('profile validation respects required names, lengths, nullable phone and supported phone formats', () => {
  assert.deepEqual(validateProfile({ firstName: 'Ada', lastName: 'Lovelace', phoneNumber: '' }), {})
  const errors = validateProfile({ firstName: '  ', lastName: 'a'.repeat(101), phoneNumber: 'a'.repeat(31) })
  assert.ok(errors.firstName && errors.lastName && errors.phoneNumber)
  for (const phone of ['+355 69 123 4567', '(555) 123-4567 ext. 12', '123 x45', '123.456']) assert.equal(validPhone(phone), true)
  for (const phone of ['abc', '123abc', '()', '123 x', '123/456']) assert.equal(validPhone(phone), false)
  assert.deepEqual(profilePayload({ firstName: ' Ada ', lastName: ' Lovelace ', phoneNumber: ' ', role: 'Administrator', email: 'ignored' }), { firstName: 'Ada', lastName: 'Lovelace', phoneNumber: null })
})

test('picture validation rejects missing, empty, oversized and unsupported files, and mismatched signatures', () => {
  assert.ok(validatePictureFile(null))
  for (const file of [{ name: 'x.png', size: 0 }, { name: 'x.jpg', size: MAX_PICTURE_SIZE + 1 }, { name: 'x.svg', size: 20 }]) assert.ok(validatePictureFile(file))
  assert.equal(validatePictureFile({ name: 'PHOTO.JPEG', size: MAX_PICTURE_SIZE }), '')
  assert.equal(matchesPictureSignature('x.png', [137, 80, 78, 71, 13, 10, 26, 10]), true)
  assert.equal(matchesPictureSignature('x.jpg', [255, 216, 255]), true)
  assert.equal(matchesPictureSignature('x.png', [255, 216, 255]), false)
})

test('picture decode checks reject corrupt images and enforce 16 million pixels; temporary URLs are released', async () => {
  const previousImage = globalThis.Image
  const previousCreate = URL.createObjectURL
  const previousRevoke = URL.revokeObjectURL
  let pixels = [4000, 4000]
  let broken = false
  let revoked = 0
  globalThis.Image = class {
    constructor() { [this.naturalWidth, this.naturalHeight] = pixels }
    decode() { return broken ? Promise.reject(new Error('corrupt')) : Promise.resolve() }
  }
  URL.createObjectURL = () => 'blob:test'
  URL.revokeObjectURL = () => { revoked++ }
  const png = new File([new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10])], 'test.png', { type: 'image/png' })
  try {
    assert.equal(await validatePicture(png), '')
    pixels = [4001, 4000]
    assert.match(await validatePicture(png), /16 million pixels/)
    broken = true
    assert.match(await validatePicture(png), /not a valid/)
    assert.equal(revoked, 3)
  } finally {
    globalThis.Image = previousImage
    URL.createObjectURL = previousCreate
    URL.revokeObjectURL = previousRevoke
  }
})

test('API sends JSON profile edits and multipart picture uploads with JWT and preserves backend errors', async () => {
  const previousFetch = globalThis.fetch
  try {
    globalThis.fetch = async (url, options) => {
      assert.equal(url, '/api/profile/me')
      assert.equal(options.method, 'PUT')
      assert.equal(options.headers.Authorization, 'Bearer test-token')
      assert.deepEqual(JSON.parse(options.body), { firstName: 'Ada', lastName: 'Lovelace', phoneNumber: null })
      return Response.json({ firstName: 'Ada' })
    }
    await apiRequest('/profile/me', { token: 'test-token', method: 'PUT', body: JSON.stringify(profilePayload({ firstName: 'Ada', lastName: 'Lovelace', phoneNumber: '' })) })
    const form = new FormData()
    form.append('File', new File(['image'], 'test.png'))
    globalThis.fetch = async (url, options) => {
      assert.equal(url, '/api/profile/me/picture')
      assert.equal(options.method, 'POST')
      assert.equal(options.headers.Authorization, 'Bearer test-token')
      assert.equal(options.headers['Content-Type'], undefined)
      assert.equal(options.body.get('File').name, 'test.png')
      return Response.json({ profilePicturePath: '/uploads/profile-pictures/test.png' })
    }
    await apiRequest('/profile/me/picture', { token: 'test-token', method: 'POST', body: form })
    for (const [status, body, message] of [[400, { errors: { FirstName: ['First name is required.'] } }, /First name is required/], [409, { errors: ['Concurrency conflict.'] }, /Concurrency conflict/], [400, { errors: ['Invalid image content.'] }, /Invalid image content/], [413, null, /2 MB/]]) {
      globalThis.fetch = async () => Response.json(body, { status })
      await assert.rejects(apiRequest('/profile/me'), message)
    }
  } finally {
    globalThis.fetch = previousFetch
  }
})
