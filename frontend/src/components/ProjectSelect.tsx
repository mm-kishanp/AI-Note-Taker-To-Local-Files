import { Select, type SelectProps } from '@mantine/core';
import { useProjects } from '../hooks/useProjects';

export interface ProjectSelectProps extends Omit<SelectProps, 'data' | 'value' | 'onChange'> {
  value: string | null;
  onChange: (projectId: string | null) => void;
  includeArchived?: boolean;
}

/** Searchable select of projects loaded from the API. */
export function ProjectSelect({ value, onChange, includeArchived = false, label = 'Project', placeholder, ...rest }: ProjectSelectProps) {
  const { data, isLoading, error } = useProjects('', includeArchived);
  const options = (data ?? []).map((p) => ({
    value: p.id,
    label: p.clientName ? `${p.name} (${p.clientName})` : p.name,
    disabled: p.status === 'Archived' && p.id !== value,
  }));
  return (
    <Select
      label={label}
      placeholder={isLoading ? 'Loading projects…' : placeholder ?? 'Choose a project'}
      data={options}
      value={value}
      onChange={onChange}
      searchable
      clearable
      nothingFoundMessage="No projects"
      error={error ? 'Could not load projects' : undefined}
      {...rest}
    />
  );
}
