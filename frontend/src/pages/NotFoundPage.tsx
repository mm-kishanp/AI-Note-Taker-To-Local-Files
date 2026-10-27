import { Button } from '@mantine/core';
import { Link } from 'react-router-dom';
import { EmptyState } from '../components/EmptyState';
import { PageHeader } from '../components/PageHeader';

export function NotFoundPage() {
  return (
    <>
      <PageHeader title="Page not found" />
      <EmptyState
        title="This page does not exist"
        description="The link may be outdated."
        action={
          <Button component={Link} to="/">
            Go to dashboard
          </Button>
        }
      />
    </>
  );
}
