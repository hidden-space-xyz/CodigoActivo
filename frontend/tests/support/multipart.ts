/** Name and text content of the `file` part of a multipart request, as MSW received it. */
export async function readMultipartFile(
  request: Request,
): Promise<{ name: string; content: string } | null> {
  const file = (await request.formData()).get('file')
  if (file === null || typeof file === 'string') return null
  return { name: file.name, content: await file.text() }
}
