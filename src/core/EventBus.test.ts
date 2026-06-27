import { describe, expect, it, vi } from 'vitest';
import { EventBus } from './EventBus';

interface TestMap {
  ping: { n: number };
  signal: void;
  other: { s: string };
}

describe('EventBus', () => {
  it('delivers payloads to subscribers of that event', () => {
    const bus = new EventBus<TestMap>();
    const fn = vi.fn();
    bus.on('ping', fn);
    bus.emit('ping', { n: 7 });
    expect(fn).toHaveBeenCalledWith({ n: 7 });
  });

  it('does not cross-deliver between events', () => {
    const bus = new EventBus<TestMap>();
    const ping = vi.fn();
    const other = vi.fn();
    bus.on('ping', ping);
    bus.on('other', other);
    bus.emit('other', { s: 'x' });
    expect(ping).not.toHaveBeenCalled();
    expect(other).toHaveBeenCalledOnce();
  });

  it('supports void (payload-less) events', () => {
    const bus = new EventBus<TestMap>();
    const fn = vi.fn();
    bus.on('signal', fn);
    bus.emit('signal');
    expect(fn).toHaveBeenCalledOnce();
  });

  it('off() stops further delivery', () => {
    const bus = new EventBus<TestMap>();
    const fn = vi.fn();
    bus.on('ping', fn);
    bus.off('ping', fn);
    bus.emit('ping', { n: 1 });
    expect(fn).not.toHaveBeenCalled();
  });

  it('once() fires exactly once', () => {
    const bus = new EventBus<TestMap>();
    const fn = vi.fn();
    bus.once('ping', fn);
    bus.emit('ping', { n: 1 });
    bus.emit('ping', { n: 2 });
    expect(fn).toHaveBeenCalledOnce();
    expect(fn).toHaveBeenCalledWith({ n: 1 });
  });

  it('clear() removes all subscriptions', () => {
    const bus = new EventBus<TestMap>();
    const fn = vi.fn();
    bus.on('ping', fn);
    bus.clear();
    bus.emit('ping', { n: 1 });
    expect(fn).not.toHaveBeenCalled();
  });

  it('tolerates a handler that unsubscribes during dispatch', () => {
    const bus = new EventBus<TestMap>();
    const calls: string[] = [];
    const a = () => {
      calls.push('a');
      bus.off('signal', a);
    };
    const b = () => calls.push('b');
    bus.on('signal', a);
    bus.on('signal', b);
    bus.emit('signal');
    bus.emit('signal');
    // `a` removes itself on first dispatch; `b` keeps firing.
    expect(calls).toEqual(['a', 'b', 'b']);
  });
});
