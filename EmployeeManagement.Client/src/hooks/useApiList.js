import { useEffect, useState } from 'react'
import { useAuth } from '../auth/useAuth'

export default function useApiList(path) {
  const { request } = useAuth()
  const [data, setData] = useState([])
  const [loading, setLoading] = useState(Boolean(path))
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)

  useEffect(() => {
    if (!path) return
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError('')
      try {
        const result = await request(path, { signal: controller.signal, cache: 'no-store' })
        if (!controller.signal.aborted) setData(result)
      } catch (failure) {
        if (!controller.signal.aborted) setError(failure.message)
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    load()
    return () => controller.abort()
  }, [path, request, revision])

  return { data, loading, error, refresh: () => setRevision((value) => value + 1) }
}
