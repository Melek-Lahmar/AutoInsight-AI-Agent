export type DataKind = 'Numeric' | 'Date' | 'Categorical' | 'Text'

export interface NumericStatistics {
  count: number
  minimum: number
  maximum: number
  mean: number
  median: number
  standardDeviation: number
  firstQuartile: number
  thirdQuartile: number
}

export interface ColumnProfile {
  name: string
  dataType: DataKind
  missingCount: number
  uniqueCount: number
  missingPercentage: number
  statistics?: NumericStatistics
  frequencies?: { value: string; count: number; percentage: number }[]
}

export interface DatasetOverview {
  id: string
  fileName: string
  rowCount: number
  columnCount: number
  duplicateRows: number
  totalMissingValues: number
  delimiter: string
  uploadedAt: string
  preview: Record<string, string | null>[]
}

export interface QualityFinding {
  code: string
  severity: string
  title: string
  explanation: string
  column?: string
  count: number
  recommendation: string
}

export interface ChartSpec {
  type: string
  title: string
  xAxis: string
  yAxis: string
  data: { label: string; value: number; secondaryValue?: number }[]
  series?: string
}

export interface UploadResult { overview: DatasetOverview; columns: ColumnProfile[] }
export interface ToolExecution { tool: string; summary: string; durationMs: number }
export interface AgentResponse {
  mode: string
  message: string
  toolExecutions: ToolExecution[]
  chart?: ChartSpec
  usedExternalProvider: boolean
}

export interface AppConfig {
  demoModeAvailable: boolean
  aiModeConfigured: boolean
  provider?: string
  model?: string
  privacy: string
}
