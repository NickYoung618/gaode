export function dataViewModel(run: any, media: any[] = []) { return { run, media: media.map(item => ({ ...item, available: item.readiness === 'Ready' })) }; }
