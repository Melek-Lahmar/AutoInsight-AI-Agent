import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Scatter, ScatterChart, Tooltip, XAxis, YAxis } from 'recharts'
import type { ChartSpec } from '../types'

export function ChartPanel({ chart }: { chart: ChartSpec }) {
  const data = chart.data.map((point, index) => ({ ...point, index }))
  return (
    <section className="chart-card" aria-label={chart.title}>
      <div className="section-heading"><div><span className="eyebrow">Visualization</span><h3>{chart.title}</h3></div><span className="chart-type">{chart.type}</span></div>
      <div className="chart-wrap">
        <ResponsiveContainer width="100%" height="100%">
          {chart.type === 'line' ? (
            <LineChart data={data} margin={{ top: 12, right: 16, left: 0, bottom: 18 }}>
              <CartesianGrid strokeDasharray="4 4" vertical={false} stroke="#e4e8ef" /><XAxis dataKey="label" tick={{ fontSize: 11 }} /><YAxis tick={{ fontSize: 11 }} /><Tooltip /><Line type="monotone" dataKey="value" stroke="#635bff" strokeWidth={3} dot={{ r: 3 }} />
            </LineChart>
          ) : chart.type === 'scatter' ? (
            <ScatterChart margin={{ top: 12, right: 16, left: 0, bottom: 18 }}>
              <CartesianGrid strokeDasharray="4 4" stroke="#e4e8ef" /><XAxis type="number" dataKey="secondaryValue" name={chart.xAxis} tick={{ fontSize: 11 }} /><YAxis type="number" dataKey="value" name={chart.yAxis} tick={{ fontSize: 11 }} /><Tooltip cursor={{ strokeDasharray: '3 3' }} /><Scatter data={data} fill="#635bff" />
            </ScatterChart>
          ) : (
            <BarChart data={data} margin={{ top: 12, right: 16, left: 0, bottom: 28 }}>
              <CartesianGrid strokeDasharray="4 4" vertical={false} stroke="#e4e8ef" /><XAxis dataKey="label" tick={{ fontSize: 11 }} angle={-15} textAnchor="end" height={55} /><YAxis allowDecimals={false} tick={{ fontSize: 11 }} /><Tooltip /><Bar dataKey="value" fill="#635bff" radius={[5, 5, 0, 0]} maxBarSize={58} />
            </BarChart>
          )}
        </ResponsiveContainer>
      </div>
    </section>
  )
}
