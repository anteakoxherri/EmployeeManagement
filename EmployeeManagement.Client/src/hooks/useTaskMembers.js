import { useEffect, useState } from 'react'
import { useAuth } from '../auth/useAuth'

// Employees can read their project teams, without accessing /employees or /users.
export default function useTaskMembers(projects, enabled) {
  const { request } = useAuth()
  const [revision, setRevision] = useState(0)
  const [result, setResult] = useState(null)
  useEffect(() => {
    if (!enabled) return
    const controller = new AbortController()
    async function load() {
      try {
        const teams = await Promise.all(projects.map((project) => request(`/projects/${project.id}/employees`, {
          signal: controller.signal, cache: 'no-store',
        })))
        const data = [...new Map(teams.flat().map((member) => [member.id, member])).values()]
        if (!controller.signal.aborted) setResult({ projects, revision, request, data, error: '' })
      } catch (failure) {
        if (!controller.signal.aborted) setResult({ projects, revision, request, data: [], error: failure.message })
      }
    }
    load()
    return () => controller.abort()
  }, [projects, enabled, request, revision])
  const current = result?.projects === projects && result?.revision === revision && result?.request === request
  return {
    data: enabled && current ? result.data : [],
    loading: enabled && !current,
    error: enabled && current ? result.error : '',
    refresh: () => setRevision((value) => value + 1),
  }
}
