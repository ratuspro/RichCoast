import Phaser from 'phaser';

export class GameScene extends Phaser.Scene {
  private player!: Phaser.Physics.Arcade.Sprite;
  private cursors!: Phaser.Types.Input.Keyboard.CursorKeys;
  private coins!: Phaser.Physics.Arcade.StaticGroup;
  private score = 0;
  private scoreText!: Phaser.GameObjects.Text;

  constructor() {
    super({ key: 'GameScene' });
  }

  create() {
    const { width, height } = this.scale;

    // --- Ground ---
    const ground = this.physics.add.staticGroup();
    for (let x = 0; x < width; x += 32) {
      ground.create(x + 16, height - 16, 'ground');
    }

    // --- Platforms (spread vertically for portrait) ---
    const platforms = this.physics.add.staticGroup();

    // Row 1 — low
    platforms.create(80,  height - 140, 'ground');
    platforms.create(112, height - 140, 'ground');
    platforms.create(144, height - 140, 'ground');

    // Row 2 — mid-low
    platforms.create(230, height - 280, 'ground');
    platforms.create(262, height - 280, 'ground');
    platforms.create(294, height - 280, 'ground');

    // Row 3 — mid
    platforms.create(100, height - 420, 'ground');
    platforms.create(132, height - 420, 'ground');
    platforms.create(164, height - 420, 'ground');

    // Row 4 — mid-high
    platforms.create(260, height - 560, 'ground');
    platforms.create(292, height - 560, 'ground');
    platforms.create(324, height - 560, 'ground');

    // Row 5 — high
    platforms.create(120, height - 680, 'ground');
    platforms.create(152, height - 680, 'ground');
    platforms.create(184, height - 680, 'ground');

    // --- Player ---
    this.player = this.physics.add.sprite(60, height - 80, 'player');
    this.player.setBounce(0.1);
    this.player.setCollideWorldBounds(true);

    this.physics.add.collider(this.player, ground);
    this.physics.add.collider(this.player, platforms);

    // --- Coins ---
    this.coins = this.physics.add.staticGroup();
    [
      [112,  height - 180],
      [262,  height - 320],
      [132,  height - 460],
      [292,  height - 600],
      [152,  height - 720],
    ].forEach(([x, y]) => {
      this.coins.create(x, y, 'coin');
    });

    this.physics.add.overlap(this.player, this.coins, this.collectCoin, undefined, this);

    // --- Input ---
    this.cursors = this.input.keyboard!.createCursorKeys();

    // Touch: tap left half = move left, right half = move right; anywhere = jump if on ground
    this.input.on('pointerdown', (p: Phaser.Input.Pointer) => {
      if (this.player.body!.blocked.down) {
        this.player.setVelocityY(-500);
        this.playJumpSound();
      }
      if (p.x < width / 2) {
        this.player.setVelocityX(-220);
      } else {
        this.player.setVelocityX(220);
      }
    });

    // --- UI ---
    this.scoreText = this.add.text(16, 16, 'Score: 0', {
      fontSize: '20px',
      color: '#ffffff',
      stroke: '#000000',
      strokeThickness: 3,
    }).setScrollFactor(0);

    // --- Camera ---
    this.cameras.main.setBounds(0, 0, width, height);
    this.cameras.main.startFollow(this.player, true, 0.08, 0.08);
  }

  update() {
    const onGround = this.player.body!.blocked.down;

    if (this.cursors.left.isDown) {
      this.player.setVelocityX(-220);
    } else if (this.cursors.right.isDown) {
      this.player.setVelocityX(220);
    } else {
      this.player.setVelocityX(0);
    }

    if (this.cursors.up.isDown && onGround) {
      this.player.setVelocityY(-500);
      this.playJumpSound();
    }
  }

  private collectCoin(_player: unknown, coin: unknown) {
    (coin as Phaser.Physics.Arcade.Image).disableBody(true, true);
    this.score += 10;
    this.scoreText.setText(`Score: ${this.score}`);
    this.playCoinSound();
  }

  // Synthesize sounds via WebAudio — no audio files needed
  private playJumpSound() {
    const ctx = (this.sound as Phaser.Sound.WebAudioSoundManager).context;
    if (!ctx) return;
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.frequency.setValueAtTime(300, ctx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(600, ctx.currentTime + 0.1);
    gain.gain.setValueAtTime(0.2, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.15);
    osc.start(ctx.currentTime);
    osc.stop(ctx.currentTime + 0.15);
  }

  private playCoinSound() {
    const ctx = (this.sound as Phaser.Sound.WebAudioSoundManager).context;
    if (!ctx) return;
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.type = 'sine';
    osc.frequency.setValueAtTime(880, ctx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(1320, ctx.currentTime + 0.08);
    gain.gain.setValueAtTime(0.3, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.12);
    osc.start(ctx.currentTime);
    osc.stop(ctx.currentTime + 0.12);
  }
}
