import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../services/api';
import type { MigrateRootRequest, SetCredentialRequest, UpdateSettingsRequest } from '../types/api';
import { qk } from './queryKeys';

export function useSettings() {
  return useQuery({ queryKey: qk.settings, queryFn: api.settings.get });
}

export function useUpdateSettings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: UpdateSettingsRequest) => api.settings.update(req),
    onSuccess: (s) => {
      qc.setQueryData(qk.settings, s);
      void qc.invalidateQueries({ queryKey: qk.dashboard });
      void qc.invalidateQueries({ queryKey: qk.projects });
      void qc.invalidateQueries({ queryKey: qk.exports });
    },
  });
}

export function useGranolaCredential() {
  return useQuery({ queryKey: qk.granolaCredential, queryFn: api.settings.granolaCredential });
}

export function useSetGranolaCredential() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: SetCredentialRequest) => api.settings.setGranolaCredential(req),
    onSuccess: (s) => {
      qc.setQueryData(qk.granolaCredential, s);
      void qc.invalidateQueries({ queryKey: qk.health });
      void qc.invalidateQueries({ queryKey: qk.dashboard });
    },
  });
}

export function useDeleteGranolaCredential() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.settings.deleteGranolaCredential(),
    onSuccess: (s) => {
      qc.setQueryData(qk.granolaCredential, s);
      void qc.invalidateQueries({ queryKey: qk.health });
      void qc.invalidateQueries({ queryKey: qk.dashboard });
    },
  });
}

export function useTestConnection() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.granola.testConnection(),
    onSettled: () => void qc.invalidateQueries({ queryKey: qk.granolaCredential }),
  });
}

export function useAiCredential() {
  return useQuery({ queryKey: qk.aiCredential, queryFn: api.settings.aiCredential });
}

export function useSetAiCredential() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: SetCredentialRequest) => api.settings.setAiCredential(req),
    onSuccess: (s) => qc.setQueryData(qk.aiCredential, s),
  });
}

export function useDeleteAiCredential() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.settings.deleteAiCredential(),
    onSuccess: (s) => qc.setQueryData(qk.aiCredential, s),
  });
}

export function useMigrateRoot() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: MigrateRootRequest) => api.exports.migrateRoot(req),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: qk.settings });
      void qc.invalidateQueries({ queryKey: qk.exports });
      void qc.invalidateQueries({ queryKey: qk.meetings });
      void qc.invalidateQueries({ queryKey: qk.projects });
    },
  });
}

export function useGranolaFolders(enabled: boolean) {
  return useQuery({ queryKey: qk.granolaFolders, queryFn: api.granola.folders, enabled, staleTime: 5 * 60_000, retry: false });
}
