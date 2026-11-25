import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  effect,
  inject,
  input,
  model,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormValueControl } from '@angular/forms/signals';
import { Editor } from '@tiptap/core';
import { StarterKit } from '@tiptap/starter-kit';

let nextId = 0;

/**
 * A small HTML editor with bold, italic, and links, for text that ends up in an email, such as a
 * signature. Binds with `[formField]` and holds the HTML as a string. The API sanitizes it again,
 * so this only shapes what can be typed.
 */
@Component({
  selector: 'app-rich-text-editor',
  styleUrl: './rich-text-editor.scss',
  templateUrl: './rich-text-editor.html',
  host: { '[class.is-disabled]': 'disabled() || readonly()' },
})
export class RichTextEditor implements FormValueControl<string> {
  private readonly _surface = viewChild.required<ElementRef<HTMLElement>>('surface');
  private readonly _linkInput = viewChild<ElementRef<HTMLInputElement>>('linkInput');
  private readonly _injector = inject(Injector);
  private _editor: Editor | null = null;

  readonly value = model('');
  readonly disabled = input(false);
  readonly readonly = input(false);
  /** The editor's accessible name; it has no visible `<label>` of its own. */
  readonly label = input.required<string>();
  readonly describedBy = input('');
  readonly touch = output<void>();

  protected readonly linkFieldId = `rte-link-${++nextId}`;
  protected readonly active = signal({ bold: false, italic: false, link: false });
  protected readonly linkOpen = signal(false);
  protected readonly linkUrl = signal('');

  constructor() {
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => {
      const editor = new Editor({
        element: this._surface().nativeElement,
        content: this.value(),
        editable: !this.disabled() && !this.readonly(),
        extensions: [
          StarterKit.configure({
            heading: false,
            blockquote: false,
            code: false,
            codeBlock: false,
            horizontalRule: false,
            strike: false,
            link: {
              openOnClick: false,
              autolink: true,
              protocols: ['mailto'],
              defaultProtocol: 'https',
            },
          }),
        ],
        editorProps: {
          attributes: {
            role: 'textbox',
            'aria-multiline': 'true',
            'aria-label': this.label(),
            ...(this.describedBy() ? { 'aria-describedby': this.describedBy() } : {}),
            class: 'rte-doc',
          },
        },
        onUpdate: ({ editor: current }) => this.value.set(current.isEmpty ? '' : current.getHTML()),
        onSelectionUpdate: ({ editor: current }) => this.syncActive(current),
        onTransaction: ({ editor: current }) => this.syncActive(current),
        onBlur: () => this.touch.emit(),
      });

      this._editor = editor;
      destroyRef.onDestroy(() => editor.destroy());
    });

    // A value set from outside, such as Discard, replaces the document; the editor's own edits already match.
    effect(() => {
      const html = this.value();
      const editor = this._editor;

      if (editor && html !== (editor.isEmpty ? '' : editor.getHTML())) {
        editor.commands.setContent(html, { emitUpdate: false });
      }
    });

    // The signals are read before the editor exists too, so the effect tracks them from its first run.
    effect(() => {
      const editable = !this.disabled() && !this.readonly();

      this._editor?.setEditable(editable);
    });
  }

  protected toggleBold(): void {
    this._editor?.chain().focus().toggleBold().run();
  }

  protected toggleItalic(): void {
    this._editor?.chain().focus().toggleItalic().run();
  }

  protected openLink(): void {
    this.linkUrl.set(String(this._editor?.getAttributes('link')['href'] ?? ''));
    this.linkOpen.set(true);
    afterNextRender(() => this._linkInput()?.nativeElement.focus(), { injector: this._injector });
  }

  protected applyLink(): void {
    const href = this.linkUrl().trim();
    const chain = this._editor?.chain().focus().extendMarkRange('link');

    if (href) {
      chain?.setLink({ href }).run();
    } else {
      chain?.unsetLink().run();
    }

    this.linkOpen.set(false);
  }

  protected removeLink(): void {
    this._editor?.chain().focus().extendMarkRange('link').unsetLink().run();
    this.linkOpen.set(false);
  }

  protected cancelLink(): void {
    this.linkOpen.set(false);
    this._editor?.commands.focus();
  }

  protected onLinkInput(event: Event): void {
    this.linkUrl.set((event.target as HTMLInputElement).value);
  }

  private syncActive(editor: Editor): void {
    this.active.set({
      bold: editor.isActive('bold'),
      italic: editor.isActive('italic'),
      link: editor.isActive('link'),
    });
  }
}
