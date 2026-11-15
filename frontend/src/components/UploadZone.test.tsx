import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { UploadZone } from './UploadZone'

describe('UploadZone', () => {
  it('passes the selected CSV to the upload handler', () => {
    const upload = vi.fn()
    render(<UploadZone onUpload={upload} busy={false} />)
    const file = new File(['name,score\nAda,98'], 'students.csv', { type: 'text/csv' })
    fireEvent.change(screen.getByLabelText('CSV file'), { target: { files: [file] } })
    expect(upload).toHaveBeenCalledWith(file)
  })

  it('shows a loading state while analysis is running', () => {
    render(<UploadZone onUpload={() => undefined} busy />)
    expect(screen.getByText('Profiling your dataset…')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Analyzing…' })).toBeDisabled()
  })
})
