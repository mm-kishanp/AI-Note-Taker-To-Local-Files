import { notifications } from '@mantine/notifications';
import { ApiError } from '../services/api';
export function notifySuccess(message, title = 'Done') {
    notifications.show({ color: 'green', title, message, autoClose: 4000 });
}
export function notifyInfo(message, title) {
    notifications.show({ color: 'blue', title, message, autoClose: 5000 });
}
export function errorMessage(err) {
    if (err instanceof ApiError) {
        const fieldMsgs = err.errors.map((e) => (e.field ? `${e.field}: ${e.message}` : e.message));
        return [err.detail ?? err.title, ...fieldMsgs].filter(Boolean).join(' — ');
    }
    if (err instanceof Error)
        return err.message;
    return String(err);
}
export function notifyError(err, title = 'Something went wrong') {
    const t = err instanceof ApiError ? err.title || title : title;
    notifications.show({ color: 'red', title: t, message: errorMessage(err), autoClose: 8000 });
}
