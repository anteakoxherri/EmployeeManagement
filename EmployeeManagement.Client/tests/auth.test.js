import assert from 'node:assert/strict'
import { test } from 'node:test'
import { parseSession } from '../src/auth/session.js'
import { apiRequest } from '../src/api/client.js'

const roleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
const now = Date.now()
function session(roles, overrides = {}) {
  const claims = { sub: 'user-1', exp: Math.floor(now / 1000) + 3600, [roleClaim]: roles, ...overrides }
  const payload = Buffer.from(JSON.stringify(claims)).toString('base64url')
  return { token: `eyJhbGciOiJIUzI1NiJ9.${payload}.c2lnbmF0dXJl`, expiresAt: new Date(now + 3600000).toISOString(), user: { id: 'user-1', firstName: 'Test' } }
}

test('accepts both backend roles, including multiple roles', () => {
  for (const roles of ['Administrator', 'Employee', ['Employee', 'Administrator']]) {
    const result = parseSession(session(roles), now)
    assert.deepEqual(result.user.roles, Array.isArray(roles) ? roles : [roles])
  }
})

test('rejects malformed, expired, future, unsupported and mismatched sessions', () => {
  for (const value of [null, {}, { token: 'broken' }, session('Admin'), session('Employee', { exp: Math.floor(now / 1000) }), session('Employee', { nbf: Math.floor(now / 1000) + 300 }), session('Employee', { sub: 'other-user' }), session('Employee', { exp: 'invalid' })]) {
    assert.equal(parseSession(value, now), null)
  }
  assert.equal(parseSession({ ...session('Employee'), expiresAt: new Date(now - 1000).toISOString() }, now), null)
})

test('API uses existing login route and Bearer header; distinguishes 401, 403 and network errors', async () => {
  const originalFetch = globalThis.fetch
  try {
    globalThis.fetch = async (url, options) => {
      assert.equal(url, '/api/auth/login')
      assert.equal(options.method, 'POST')
      assert.equal(options.headers['Content-Type'], 'application/json')
      assert.deepEqual(JSON.parse(options.body), { email: 'test@example.com', password: 'test' })
      return new Response(JSON.stringify({ token: 'response-token' }), { status: 200 })
    }
    assert.deepEqual(await apiRequest('/auth/login', { method: 'POST', body: JSON.stringify({ email: 'test@example.com', password: 'test' }) }), { token: 'response-token' })
    let expired = false
    globalThis.fetch = async (_url, options) => {
      assert.equal(options.headers.Authorization, 'Bearer test-token')
      return new Response(null, { status: 401 })
    }
    await assert.rejects(apiRequest('/profile/me', { token: 'test-token', onUnauthorized: () => { expired = true } }), /invalid or expired/)
    assert.equal(expired, true)
    globalThis.fetch = async () => new Response(null, { status: 401 })
    await assert.rejects(apiRequest('/auth/login'), /Incorrect email or password/)
    expired = false
    globalThis.fetch = async () => new Response(null, { status: 403 })
    await assert.rejects(apiRequest('/users', { onUnauthorized: () => { expired = true } }), /permission/)
    assert.equal(expired, false)
    globalThis.fetch = async () => { throw new TypeError('offline') }
    await assert.rejects(apiRequest('/auth/login'), /Cannot connect/)
  } finally {
    globalThis.fetch = originalFetch
  }
})
