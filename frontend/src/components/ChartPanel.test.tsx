import type { ReactNode } from 'react'
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ChartPanel } from './ChartPanel'

vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  BarChart: ({ children }: { children: ReactNode }) => <div data-testid="bar-chart">{children}</div>,
  Bar: () => null, CartesianGrid: () => null, Tooltip: () => null,
  XAxis: ({ dataKey }: { dataKey?: string }) => <span data-testid="x-axis-key">{dataKey}</span>, YAxis: () => null,
  LineChart: ({ children }: { children: ReactNode }) => <div>{children}</div>, Line: () => null,
  ScatterChart: ({ children }: { children: ReactNode }) => <div>{children}</div>, Scatter: () => null,
}))

describe('ChartPanel', () => {
  it('renders a backend chart specification', () => {
    render(<ChartPanel chart={{ type: 'bar', title: 'Region distribution', xAxis: 'Region', yAxis: 'Count', data: [{ label: 'North', value: 3 }] }} />)
    expect(screen.getByText('Region distribution')).toBeInTheDocument()
    expect(screen.getByTestId('bar-chart')).toBeInTheDocument()
  })

  it('plots scatter charts using the numeric x value from the backend', () => {
    render(<ChartPanel chart={{ type: 'scatter', title: 'Score by Hours', xAxis: 'Hours', yAxis: 'Score', data: [{ label: '5', value: 82, secondaryValue: 5 }] }} />)
    expect(screen.getByTestId('x-axis-key')).toHaveTextContent('secondaryValue')
  })
})
