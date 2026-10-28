import { jsx as _jsx } from "react/jsx-runtime";
import { Select } from '@mantine/core';
import { useProjects } from '../hooks/useProjects';
/** Searchable select of projects loaded from the API. */
export function ProjectSelect({ value, onChange, includeArchived = false, label = 'Project', placeholder, ...rest }) {
    const { data, isLoading, error } = useProjects('', includeArchived);
    const options = (data ?? []).map((p) => ({
        value: p.id,
        label: p.clientName ? `${p.name} (${p.clientName})` : p.name,
        disabled: p.status === 'Archived' && p.id !== value,
    }));
    return (_jsx(Select, { label: label, placeholder: isLoading ? 'Loading projects…' : placeholder ?? 'Choose a project', data: options, value: value, onChange: onChange, searchable: true, clearable: true, nothingFoundMessage: "No projects", error: error ? 'Could not load projects' : undefined, ...rest }));
}
