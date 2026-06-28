const params = new URLSearchParams(window.location.search);
let _debug = params.get('debug') === '2';

export function isDebug(): boolean {
  return _debug;
}

export function toggleDebug(): void {
  _debug = !_debug;
}
