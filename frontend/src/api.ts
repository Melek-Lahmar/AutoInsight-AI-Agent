import type { AgentResponse, AppConfig, ChartSpec, QualityFinding, UploadResult } from './types'

const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, init)
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.detail ?? `Request failed with status ${response.status}.`)
  }
  return response.json() as Promise<T>
}

export const api = {
  getConfig: () => request<AppConfig>('/api/config'),
  upload: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<UploadResult>('/api/datasets/upload', { method: 'POST', body: form })
  },
  quality: (id: string) => request<QualityFinding[]>(`/api/datasets/${id}/quality`),
  visualization: (id: string, body: { type: string; xColumn: string; yColumn?: string; bins?: number }) =>
    request<ChartSpec>(`/api/datasets/${id}/visualizations`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    }),
  chat: (datasetId: string, message: string, mode: string) => request<AgentResponse>('/api/agent/chat', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ datasetId, message, mode }),
  }),
  report: async (datasetId: string) => {
    const response = await fetch(`${API_URL}/api/reports/generate`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ datasetId }),
    })
    if (!response.ok) throw new Error((await response.json().catch(() => null))?.detail ?? 'Report generation failed.')
    return response.blob()
  },
}
