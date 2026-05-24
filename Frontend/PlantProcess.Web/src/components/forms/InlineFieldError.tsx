type InlineFieldErrorProps = {
  id: string;
  message?: string;
};

export function InlineFieldError({ id, message }: InlineFieldErrorProps) {
  if (!message) return null;

  return (
    <p id={id} className="inline-field-error" role="alert">
      {message}
    </p>
  );
}