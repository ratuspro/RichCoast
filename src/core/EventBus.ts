import type { GameEventMap } from './contracts';

type Handler<T> = (payload: T) => void;

/**
 * Minimal typed pub/sub — the only thing the two halves share at runtime.
 *
 * Phaser-free so it runs under Vitest in Node, and so zones stay decoupled: a
 * system knows event names and payload shapes, never another zone's class.
 *
 * Generic over an event map (defaults to the game's {@link GameEventMap}) so it
 * can also be exercised in isolation by tests.
 */
export class EventBus<TMap = GameEventMap> {
  private readonly handlers: { [K in keyof TMap]?: Set<Handler<TMap[K]>> } = {};

  on<K extends keyof TMap>(event: K, handler: Handler<TMap[K]>): this {
    (this.handlers[event] ??= new Set<Handler<TMap[K]>>()).add(handler);
    return this;
  }

  once<K extends keyof TMap>(event: K, handler: Handler<TMap[K]>): this {
    const wrapper: Handler<TMap[K]> = (payload) => {
      this.off(event, wrapper);
      handler(payload);
    };
    return this.on(event, wrapper);
  }

  off<K extends keyof TMap>(event: K, handler: Handler<TMap[K]>): this {
    this.handlers[event]?.delete(handler);
    return this;
  }

  emit<K extends keyof TMap>(
    event: K,
    ...args: TMap[K] extends void ? [] : [payload: TMap[K]]
  ): this {
    const set = this.handlers[event];
    if (!set) return this;
    const payload = args[0] as TMap[K];
    // Iterate a copy so a handler that subscribes/unsubscribes mid-dispatch
    // can't corrupt the loop.
    for (const handler of [...set]) handler(payload);
    return this;
  }

  /** Drop every subscription. Called on scene shutdown / mode switch. */
  clear(): this {
    for (const key of Object.keys(this.handlers) as (keyof TMap)[]) {
      delete this.handlers[key];
    }
    return this;
  }
}
