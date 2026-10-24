#!/usr/bin/env python3
"""
Generate a clean, professional, readable PDF guide for exam/defense in the project root.
"""
from pathlib import Path
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, PageBreak, Table, TableStyle, KeepTogether, HRFlowable
)
from reportlab.pdfgen import canvas

# 1. Register Cyrillic TrueType Fonts
FONT_REGULAR = '/System/Library/Fonts/Supplemental/Arial.ttf'
FONT_BOLD = '/System/Library/Fonts/Supplemental/Arial Bold.ttf'
FONT_ITALIC = '/System/Library/Fonts/Supplemental/Arial Italic.ttf'
FONT_BOLD_ITALIC = '/System/Library/Fonts/Supplemental/Arial Bold Italic.ttf'

pdfmetrics.registerFont(TTFont('Arial', FONT_REGULAR))
pdfmetrics.registerFont(TTFont('Arial-Bold', FONT_BOLD))
pdfmetrics.registerFont(TTFont('Arial-Italic', FONT_ITALIC))
pdfmetrics.registerFont(TTFont('Arial-BoldItalic', FONT_BOLD_ITALIC))
pdfmetrics.registerFontFamily('Arial', normal='Arial', bold='Arial-Bold', italic='Arial-Italic', boldItalic='Arial-BoldItalic')

# Colors
PRIMARY = colors.HexColor('#1E3A8A')     # Navy / Dark Blue
SECONDARY = colors.HexColor('#2563EB')   # Accent Blue
DARK_TEXT = colors.HexColor('#1F2937')   # Neutral Dark
LIGHT_BG = colors.HexColor('#F3F4F6')    # Soft Light Grey
CARD_BG = colors.HexColor('#EFF6FF')     # Light blue card
CODE_BG = colors.HexColor('#F8FAFC')     # Code background
BORDER_COL = colors.HexColor('#E2E8F0')  # Border
GREEN_ACCENT = colors.HexColor('#059669')# Accent Green
RED_ACCENT = colors.HexColor('#DC2626')  # Accent Red

# Styles
styles = {
    'Title': ParagraphStyle('Title', fontName='Arial-Bold', fontSize=20, leading=24, textColor=PRIMARY, alignment=1, spaceAfter=8),
    'Subtitle': ParagraphStyle('Subtitle', fontName='Arial', fontSize=10.5, leading=15, textColor=colors.HexColor('#4B5563'), alignment=1, spaceAfter=14),
    
    'H1': ParagraphStyle('H1', fontName='Arial-Bold', fontSize=13.5, leading=17, textColor=PRIMARY, spaceBefore=12, spaceAfter=6, keepWithNext=True),
    'H2': ParagraphStyle('H2', fontName='Arial-Bold', fontSize=11, leading=15, textColor=SECONDARY, spaceBefore=8, spaceAfter=4, keepWithNext=True),
    
    'Body': ParagraphStyle('Body', fontName='Arial', fontSize=9, leading=12.5, textColor=DARK_TEXT, spaceAfter=4),
    'BodyBold': ParagraphStyle('BodyBold', fontName='Arial-Bold', fontSize=9, leading=12.5, textColor=DARK_TEXT, spaceAfter=4),
    'Bullet': ParagraphStyle('Bullet', fontName='Arial', fontSize=8.8, leading=12, textColor=DARK_TEXT, leftIndent=12, spaceAfter=2),
    
    'CodeBlock': ParagraphStyle('CodeBlock', fontName='Arial', fontSize=8, leading=10.5, textColor=colors.HexColor('#0F172A')),
    'CmdBlock': ParagraphStyle('CmdBlock', fontName='Arial-Bold', fontSize=8, leading=11, textColor=colors.HexColor('#1E293B')),
    
    'TableHead': ParagraphStyle('TableHead', fontName='Arial-Bold', fontSize=8, leading=10.5, textColor=colors.white, alignment=1),
    'TableCell': ParagraphStyle('TableCell', fontName='Arial', fontSize=7.8, leading=10, textColor=DARK_TEXT),
    'TableCellBold': ParagraphStyle('TableCellBold', fontName='Arial-Bold', fontSize=7.8, leading=10, textColor=DARK_TEXT),
    'TableCellCenter': ParagraphStyle('TableCellCenter', fontName='Arial', fontSize=7.8, leading=10, textColor=DARK_TEXT, alignment=1),
    
    'QTitle': ParagraphStyle('QTitle', fontName='Arial-Bold', fontSize=9.5, leading=13, textColor=PRIMARY, keepWithNext=True),
    'QAnswer': ParagraphStyle('QAnswer', fontName='Arial', fontSize=8.5, leading=11.5, textColor=DARK_TEXT),
    'QShort': ParagraphStyle('QShort', fontName='Arial-Bold', fontSize=8.5, leading=11.5, textColor=colors.HexColor('#065F46')),
}

class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_header_footer(num_pages)
            super().showPage()
        super().save()

    def draw_header_footer(self, page_count):
        self.saveState()
        self.setFont('Arial', 8)
        self.setFillColor(colors.HexColor('#6B7280'))
        
        # Header (pages > 1)
        if self._pageNumber > 1:
            self.drawString(15*mm, 285*mm, "Методичка для подготовки к сдаче — UDP Game Protocol")
            self.setStrokeColor(BORDER_COL)
            self.setLineWidth(0.5)
            self.line(15*mm, 283*mm, 195*mm, 283*mm)
            
        # Footer
        page_text = f"Стр. {self._pageNumber} из {page_count}"
        self.drawRightString(195*mm, 10*mm, page_text)
        self.drawString(15*mm, 10*mm, "Разработка многопользовательских приложений VR | ПР №1–№3")
        self.setStrokeColor(BORDER_COL)
        self.setLineWidth(0.5)
        self.line(15*mm, 13*mm, 195*mm, 13*mm)
        self.restoreState()

def build_pdf(target_path: Path):
    doc = SimpleDocTemplate(
        str(target_path),
        pagesize=A4,
        leftMargin=14*mm,
        rightMargin=14*mm,
        topMargin=16*mm,
        bottomMargin=16*mm
    )
    
    story = []
    
    # Header block
    story.append(Paragraph("Методичка для подготовки к сдаче", styles['Title']))
    story.append(Paragraph("<b>Игровой UDP-протокол: от базового обмена до адаптивной надёжной доставки (ПР №1–№3)</b><br/>Стек: C# / .NET 10 | Authoritative Dedicated Server | loopback UDP", styles['Subtitle']))
    story.append(HRFlowable(width="100%", thickness=1, color=PRIMARY, spaceAfter=8, spaceBefore=0))
    
    # 1. СУТЬ ПРОЕКТА
    story.append(Paragraph("1. Экспресс-суть проекта: как развивалась система", styles['H1']))
    summary_text = (
        "Проект реализует бинарный клиент-серверный сетевой протокол реального времени поверх <b>UDP</b> на C# (.NET 10). "
        "Архитектура строится вокруг <b>Authoritative Dedicated Server</b> (сервер единолично проверяет правила, считает выстрелы и задаёт координаты игрока). "
        "Проект развивался через 3 практические работы:"
    )
    story.append(Paragraph(summary_text, styles['Body']))
    
    evolution_data = [
        [
            Paragraph("<b>ПР №1: Базовый обмен</b>", styles['TableCellBold']),
            Paragraph("<b>ПР №2: Телеметрия сети</b>", styles['TableCellBold']),
            Paragraph("<b>ПР №3: Надёжная доставка</b>", styles['TableCellBold'])
        ],
        [
            Paragraph("• Клиент и сервер по сокетам UDP.<br/>• Команды <code>MOVEMENT</code> и <code>SHOOT</code>.<br/>• Ответ сервера <code>STATE_UPDATE</code>.<br/>• Big-Endian сериализация (BinaryPrimitives).", styles['TableCell']),
            Paragraph("• Измерения пакетами <code>PING</code> / <code>PONG</code>.<br/>• Расчёт RTT, сглаженного SRTT, Jitter.<br/>• Эмулятор сети (loss, delay, jitter).<br/>• 6 профилей тестов по 50 пакетов.", styles['TableCell']),
            Paragraph("• Селективная надёжность: <code>SHOOT</code> + <code>ACK</code>.<br/>• Адаптивный тайм-аут RTO (RFC 6298).<br/>• Алгоритм Карна (исключение RTT retry).<br/>• Окно дедупликации сервера (1024 ID).", styles['TableCell'])
        ]
    ]
    t_evol = Table(evolution_data, colWidths=[58*mm, 61*mm, 63*mm])
    t_evol.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), LIGHT_BG),
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('INNERGRID', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_evol)
    story.append(Spacer(1, 4))
    
    story.append(Paragraph("<b>Модули решения (Solution):</b>", styles['BodyBold']))
    story.append(Paragraph("• <b>UdpGame.Protocol</b>: структуры пакетов, валидация полей и типов, строгая Big-Endian сериализация.", styles['Bullet']))
    story.append(Paragraph("• <b>UdpGame.Transport</b>: абстракция <code>UdpTransport</code> над UdpClient и эмулятор задержек/потерь <code>NetworkEmulator</code>.", styles['Bullet']))
    story.append(Paragraph("• <b>UdpGame.Telemetry</b>: учёт пакетов в полёте (<code>inFlight</code>), вычисление RTT, SRTT (α=0.125), Jitter и процента потерь.", styles['Bullet']))
    story.append(Paragraph("• <b>UdpGame.Reliability</b>: <code>ReliableChannel</code> (очереди pending/failed, повторные отправки) и <code>AdaptiveTimeout</code>.", styles['Bullet']))
    story.append(Paragraph("• <b>UdpGame.Server</b>: состояние игрока (координаты, выстрелы), PONG и дедупликация <code>RecentCommandWindow</code>.", styles['Bullet']))
    story.append(Paragraph("• <b>UdpGame.Client</b>: организация игрового цикла, отправка команд и сценарии экспериментов.", styles['Bullet']))
    
    # 2. РАЗБОР ПР1, ПР2, ПР3
    story.append(Spacer(1, 4))
    story.append(Paragraph("2. Технический разбор практических работ", styles['H1']))
    
    story.append(Paragraph("Практическая работа №1: Базовый UDP-протокол", styles['H2']))
    story.append(Paragraph(
        "Топология — авторитетный сервер. Клиент шлёт пакеты <code>MOVEMENT</code> (координаты float x, y, z в диапазоне [-1000; 1000]) "
        "и <code>SHOOT</code> (WeaponId 1–3). Сервер проверяет границы, меняет внутреннее состояние и возвращает <code>STATE_UPDATE</code> "
        "с тем же <code>SequenceNumber</code>. Все числовые поля передаются в <b>сетевом порядке байт (Big-Endian)</b>, что обеспечивает платформонезависимость.",
        styles['Body']
    ))
    
    story.append(Paragraph("Практическая работа №2: Измерение характеристик канала (Телеметрия)", styles['H2']))
    story.append(Paragraph(
        "Для оценки сети клиент шлёт <code>PING</code> с клиентской меткой времени (<code>Stopwatch</code>, микросекунды), сервер зеркалит её в <code>PONG</code>. "
        "RTT вычисляется <b>строго по часам клиента</b>: <code>RTT = t_recv - t_send</code> (серверные часы не используются, чтобы исключить рассинхронизацию системного времени).<br/>"
        "• <b>SRTT (сглаженная задержка):</b> <code>SRTT = 0.875 * SRTT_prev + 0.125 * RTT_new</code> (вес α = 0.125).<br/>"
        "• <b>Jitter (джиттер):</b> среднее абсолютных разностей соседних RTT: <code>J = Σ|RTT_i - RTT_{i-1}| / (N - 1)</code>.<br/>"
        "• <b>Потери (Loss Rate):</b> доля пакетов, не вернувшихся за фиксированный таймаут 1000 мс (<code>N_timeout / N_sent * 100%</code>).",
        styles['Body']
    ))
    
    story.append(Paragraph("Практическая работа №3: Надёжная доставка и адаптивный тайм-аут", styles['H2']))
    story.append(Paragraph(
        "Реализована выборочная надёжность критичных событий: выстрел <code>SHOOT</code> сделан надёжным (<code>RequiresAck = 1</code>), "
        "а частые координаты <code>MOVEMENT</code>, пинги и ответы оставлены ненадёжными.<br/>"
        "• <b>Заголовок v2 (8 байт):</b> на смещении 7 добавлен байт <code>RequiresAck</code> (0 или 1). Введён тип пакета <code>ACK</code> (код 6, размер 10 байт).<br/>"
        "• <b>Адаптивный RTO (RFC 6298):</b> тайм-аут подстраивается под сеть: <code>RTTVAR = 0.75 * RTTVAR + 0.25 * |SRTT - RTT|</code> (β = 0.25); "
        "<code>RTO = clamp(SRTT + 4 * RTTVAR, 100 мс, 3000 мс)</code>.<br/>"
        "• <b>Алгоритм Карна:</b> если пакет ретранслировался (попытка > 1), пришедший ACK <b>не используется</b> для обновления RTO (неизвестно, к какой попытке он относится).<br/>"
        "• <b>Серверная дедупликация (1024 ID):</b> при получении повторного SHOOT сервер не делает повторный выстрел, но повторно шлёт ACK и сохранённый STATE_UPDATE.<br/>"
        "• <b>Политика Failed:</b> максимум 5 попыток (1 исходная + 4 повтора). Если ACK не пришёл — статус <code>failed</code>, событие логируется, игра продолжается.",
        styles['Body']
    ))
    
    # 3. ФОРМАТ ПАКЕТОВ
    story.append(Spacer(1, 4))
    story.append(Paragraph("3. Сводка формата пакетов (Wire Specification v2)", styles['H1']))
    story.append(Paragraph("Общий заголовок дейтаграммы — строго <b>8 байт</b> (Big-Endian): [PacketType: 1B] [SequenceNumber: 2B] [PayloadSize: 2B] [ProtocolVersion=2: 2B] [RequiresAck: 1B].", styles['Body']))
    
    packet_table_data = [
        [
            Paragraph("Тип пакета", styles['TableHead']),
            Paragraph("Код", styles['TableHead']),
            Paragraph("Направление", styles['TableHead']),
            Paragraph("Назначение и полезная нагрузка (Payload)", styles['TableHead']),
            Paragraph("Размер", styles['TableHead']),
            Paragraph("Ack?", styles['TableHead']),
        ],
        [
            Paragraph("<code>MOVEMENT</code>", styles['TableCellBold']),
            Paragraph("1", styles['TableCellCenter']),
            Paragraph("Client → Server", styles['TableCell']),
            Paragraph("Координаты игрока (X, Y, Z — 3 float по 4 байта = 12 байт)", styles['TableCell']),
            Paragraph("20 байт", styles['TableCellCenter']),
            Paragraph("Нет (0)", styles['TableCellCenter']),
        ],
        [
            Paragraph("<code>SHOOT</code>", styles['TableCellBold']),
            Paragraph("2", styles['TableCellCenter']),
            Paragraph("Client → Server", styles['TableCell']),
            Paragraph("Выстрел игрока: WeaponId (UInt8 = 1 байт)", styles['TableCell']),
            Paragraph("9 байт", styles['TableCellCenter']),
            Paragraph("<b>Да (1)</b>", styles['TableCellCenter']),
        ],
        [
            Paragraph("<code>STATE_UPDATE</code>", styles['TableCellBold']),
            Paragraph("3", styles['TableCellCenter']),
            Paragraph("Server → Client", styles['TableCell']),
            Paragraph("Ответ сервера: AckType, Status, X, Y, Z, ShotsFired, LastWeapon (19 байт)", styles['TableCell']),
            Paragraph("27 байт", styles['TableCellCenter']),
            Paragraph("Нет (0)", styles['TableCellCenter']),
        ],
        [
            Paragraph("<code>PING</code>", styles['TableCellBold']),
            Paragraph("4", styles['TableCellCenter']),
            Paragraph("Client → Server", styles['TableCell']),
            Paragraph("Зондирование сети: ClientSendTimeUs (UInt64 = 8 байт)", styles['TableCell']),
            Paragraph("16 байт", styles['TableCellCenter']),
            Paragraph("Нет (0)", styles['TableCellCenter']),
        ],
        [
            Paragraph("<code>PONG</code>", styles['TableCellBold']),
            Paragraph("5", styles['TableCellCenter']),
            Paragraph("Server → Client", styles['TableCell']),
            Paragraph("Эхо-ответ: ClientSendTime, ServerRecvTime, ServerSendTime (24 байта)", styles['TableCell']),
            Paragraph("32 байта", styles['TableCellCenter']),
            Paragraph("Нет (0)", styles['TableCellCenter']),
        ],
        [
            Paragraph("<code>ACK</code>", styles['TableCellBold']),
            Paragraph("6", styles['TableCellCenter']),
            Paragraph("Server → Client", styles['TableCell']),
            Paragraph("Подтверждение доставки: AcknowledgedSequence (UInt16 = 2 байта)", styles['TableCell']),
            Paragraph("10 байт", styles['TableCellCenter']),
            Paragraph("Нет (0)", styles['TableCellCenter']),
        ],
    ]
    t_pkt = Table(packet_table_data, colWidths=[24*mm, 10*mm, 28*mm, 82*mm, 18*mm, 20*mm])
    t_pkt.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), PRIMARY),
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('INNERGRID', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT_BG]),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_pkt)
    
    # 4. ВОПРОСЫ ДЛЯ ЗАЩИТЫ
    story.append(Spacer(1, 6))
    story.append(Paragraph("4. Вопросы для защиты с готовыми ответами", styles['H1']))
    story.append(Paragraph("Каждый вопрос разобран в двух форматах: подробный технический ответ и концентрированная формулировка для быстрого ответа у доски.", styles['Body']))
    
    questions = [
        (
            "1. Почему фиксированный тайм-аут из ПР №2 непригоден напрямую для повторной передачи?",
            "В ПР №2 фиксированный тайм-аут 1000 мс использовался лишь как критерий признания PING потерянным. "
            "Если использовать его для повторных отправок: на быстрых каналах (RTT < 5 мс) мы заставим игрока ждать целую секунду до повтора, что разрушит геймплей; "
            "на медленных каналах (RTT > 1000 мс) тайм-аут истечет до прихода ответа, породив лавину ложных ретрансмиссий (spurious retransmissions) и перегрузив сеть.",
            "Коротко: Фиксированный тайм-аут не знает реального RTT сети: на быстрой сети он слишком долго ждёт, а на медленной вызывает паразитные ложные повторы."
        ),
        (
            "2. Как рассчитывается RTTVAR и почему коэффициент при нём равен 4, а не 1?",
            "RTTVAR — оценка дисперсии/разброса RTT (линейное среднее отклонение от SRTT): <code>RTTVAR = 0.75*RTTVAR + 0.25*|SRTT - RTT|</code>. "
            "Формула RTO: <code>RTO = SRTT + 4 * RTTVAR</code>. Если взять множитель 1, то при первом же естественном колебании задержки (джиттере) RTO сработает раньше "
            "прихода ответа. Множитель 4 взят из RFC 6298 (аналог правила 3-х сигм): он гарантирует запас прочности, покрывающий 99%+ случайных флуктуаций задержки.",
            "Коротко: RTTVAR оценивает джиттер. Множитель 4 дает статистический запас прочности от преждевременного срабатывания тайм-аута при скачках задержки."
        ),
        (
            "3. Почему ACK не требует собственного подтверждения (нет цепочки ACK на ACK)?",
            "Если бы на ACK требовалось слать ACK, то на него тоже потребовался бы ACK — возникнет бесконечная рекурсия («проблема двух генералов»). "
            "Вместо этого протокол гарантирует надёжность за счёт отправителя исходных данных: если ACK потерялся, у клиента срабатывает RTO, "
            "он повторяет исходный SHOOT, а сервер повторно высылает ACK. Сам ACK всегда отправляется с RequiresAck = 0.",
            "Коротко: Иначе возникнет бесконечная цепочка подтверждений. Потеря самого ACK компенсируется повторной отправкой исходного пакета."
        ),
        (
            "4. Как сервер отличает повторно присланный пакет (retransmission) от нового выстрела?",
            "По заголовку <code>SequenceNumber</code>. При retransmission клиент отправляет точную копию байт с тем же номером. "
            "Сервер хранит структуру <code>RecentCommandWindow</code> (1024 ID для каждого клиента). Если номер уже есть в окне, сервер видит дубликат: "
            "он не выполняет выстрел повторно (счётчик ShotsFired не растет), но повторно шлёт ACK и сохранённую копию STATE_UPDATE.",
            "Коротко: По номеру SequenceNumber. Повтор сохраняет тот же номер; сервер сверяет его с окном (1024) и повторно шлёт ACK без повтора выстрела."
        ),
        (
            "5. Что должно произойти в игре, если команда SHOOT окончательно не доставлена (failed)?",
            "После 5 попыток (1 отправка + 4 повтора) пакет удаляется из очереди pending и переходит в статус failed. "
            "Игра не падает: ошибка логируется на прикладном уровне (UI может показать ошибку выстрела или вернуть патрон). "
            "Важно: статус failed означает лишь отсутствие подтверждения клиенту, но не гарантирует отсутствие эффекта на сервере (например, сервер выстрел засчитал, но все ACK потерялись).",
            "Коротко: Пакет логируется как недоставленный, игра работает дальше. Failed означает отсутствие подтверждения клиенту, а не отмену действия на сервере."
        ),
        (
            "6. Почему не все команды имеет смысл делать надёжными (например, MOVEMENT)?",
            "SHOOT — дискретное ключевое действие, потеря которого искажает игру. MOVEMENT — высокочастотный поток координат. "
            "Если пакет с координатами x=2 потерялся, но сервер уже получил x=3 и x=4, повторная доставка старого пакета x=2 вредна (отбросит игрока назад во времени). "
            "Для движения критична актуальность, а не сохранность истории. Кроме того, ACK на каждый шаг перегрузили бы канал.",
            "Коротко: Координаты быстро устаревают: свежий пакет важнее потерянного старого. А выстрел — уникальное критичное событие, которое терять нельзя."
        ),
        (
            "7. Как результаты ПР №2 (RTT, джиттер) связаны с параметрами retransmission в ПР №3?",
            "ПР №2 заложила измерительную базу: телеметрию и алгоритмы оценки RTT и джиттера. ПР №3 использует эти измерения в формуле AdaptiveTimeout. "
            "Чем выше RTT и джиттер сети, тем выше поднимается RTO перед повторной отправкой. Также работает алгоритм Карна: "
            "ACK на повторные пакеты не обучают RTO, исключая искажение оценки задержки.",
            "Коротко: ПР №2 дает метрики задержки и джиттера, а ПР №3 подстраивает под них время ожидания RTO перед повторной отправкой."
        ),
    ]
    
    for q_title, q_ans, q_short in questions:
        card_content = [
            Paragraph(f"<b>{q_title}</b>", styles['QTitle']),
            Spacer(1, 1),
            Paragraph(q_ans, styles['QAnswer']),
            Spacer(1, 1),
            Paragraph(f"<b>Ответ у доски:</b> {q_short}", styles['QShort'])
        ]
        card_table = Table([[card_content]], colWidths=[182*mm])
        card_table.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,-1), CARD_BG),
            ('BOX', (0,0), (-1,-1), 0.5, SECONDARY),
            ('TOPPADDING', (0,0), (-1,-1), 4),
            ('BOTTOMPADDING', (0,0), (-1,-1), 4),
            ('LEFTPADDING', (0,0), (-1,-1), 6),
            ('RIGHTPADDING', (0,0), (-1,-1), 6),
        ]))
        story.append(card_table)
        story.append(Spacer(1, 3))
        
    # 5. КОМАНДЫ ДЛЯ СДАЧИ
    story.append(Spacer(1, 4))
    story.append(Paragraph("5. Шпаргалка команд для живой демонстрации", styles['H1']))
    
    cmds_data = [
        [
            Paragraph("Задача", styles['TableHead']),
            Paragraph("Команда терминала (из корня проекта)", styles['TableHead']),
            Paragraph("Что ожидать на экране", styles['TableHead'])
        ],
        [
            Paragraph("<b>Сборка и тесты</b>", styles['TableCellBold']),
            Paragraph("<code>dotnet test UdpGame.sln</code>", styles['CmdBlock']),
            Paragraph("<b>Все 78 тестов успешно пройдены</b> (0 failed)", styles['TableCell'])
        ],
        [
            Paragraph("<b>ПР №1: Сервер</b>", styles['TableCellBold']),
            Paragraph("<code>dotnet run --project src/UdpGame.Server -- --port 27015 --log server.log</code>", styles['CmdBlock']),
            Paragraph("Сервер слушает порт 27015, пишет лог", styles['TableCell'])
        ],
        [
            Paragraph("<b>ПР №1: Клиент</b>", styles['TableCellBold']),
            Paragraph("<code>dotnet run --project src/UdpGame.Client -- --host 127.0.0.1 --port 27015 --count 6</code>", styles['CmdBlock']),
            Paragraph("6/6 успешных ответов (чередование движения и стрельбы)", styles['TableCell'])
        ],
        [
            Paragraph("<b>ПР №2: Эксперимент</b>", styles['TableCellBold']),
            Paragraph("<code>dotnet run --project src/UdpGame.Client -- --experiment --csv docs/latency_samples_new.csv</code>", styles['CmdBlock']),
            Paragraph("6 серий по 50 PING (расчёт RTT/SRTT/Jitter/Loss)", styles['TableCell'])
        ],
        [
            Paragraph("<b>ПР №3: Эксперимент</b>", styles['TableCellBold']),
            Paragraph("<code>dotnet run --project src/UdpGame.Client -- --reliability-experiment --csv docs/reliability_samples_new.csv</code>", styles['CmdBlock']),
            Paragraph("6 серий по 50 SHOOT (повторы, адаптивный RTO, ACK)", styles['TableCell'])
        ],
        [
            Paragraph("<b>Анализ данных</b>", styles['TableCellBold']),
            Paragraph("<code>python3 scripts/analyze_reliability.py</code>", styles['CmdBlock']),
            Paragraph("Математическая проверка RTO, Карна и дедупликации", styles['TableCell'])
        ],
    ]
    t_cmd = Table(cmds_data, colWidths=[32*mm, 105*mm, 45*mm])
    t_cmd.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), PRIMARY),
        ('BOX', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('INNERGRID', (0,0), (-1,-1), 0.5, BORDER_COL),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, LIGHT_BG]),
        ('TOPPADDING', (0,0), (-1,-1), 3),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3),
        ('LEFTPADDING', (0,0), (-1,-1), 4),
        ('RIGHTPADDING', (0,0), (-1,-1), 4),
    ]))
    story.append(t_cmd)
    
    # 6. ЧИСЛА РЕАЛЬНОГО ЭКСПЕРИМЕНТА
    story.append(Spacer(1, 4))
    story.append(Paragraph("6. Ключевые цифры реального эксперимента (ПР №3)", styles['H1']))
    story.append(Paragraph(
        "В реальном эксперименте выполнены 6 серий по 50 команд (суммарно <b>300 исходных SHOOT</b>):<br/>"
        "• <b>299 подтверждены ACK</b>, 255 доставлены с 1-й попытки, <b>59 повторов (retransmissions)</b>, <b>ровно 1 failed</b> (в серии loss_20).<br/>"
        "• Тот самый 1 failed (команда №6, sequence=312) продемонстрировал важное поведение: сервер применил выстрел (ShotsFired=6), "
        "но из-за потерь в обратном канале все 5 ACK не дошли до клиента. Failed означает отсутствие подтверждения, а не отмену эффекта.<br/>"
        "• Сервер зафиксировал <b>29 дубликатов</b> — RecentCommandWindow корректно защитило игровой мир от повторного начисления выстрелов.",
        styles['Body']
    ))
    
    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"PDF built successfully at {target_path}")

if __name__ == '__main__':
    root = Path(__file__).resolve().parent.parent
    target = root / "МЕТОДИЧКА_ДЛЯ_СДАЧИ.pdf"
    build_pdf(target)
