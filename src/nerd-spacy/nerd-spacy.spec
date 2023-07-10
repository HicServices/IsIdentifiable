import platform
import PyInstaller

block_cipher = None

datas = []

# spaCy
datas.extend(PyInstaller.utils.hooks.collect_data_files('spacy.lang', include_py_files = True))
datas.extend(PyInstaller.utils.hooks.copy_metadata('spacy_lookups_data'))
datas.extend(PyInstaller.utils.hooks.collect_data_files('spacy_lookups_data', include_py_files = True))
datas.extend(PyInstaller.utils.hooks.collect_data_files('en_core_web_sm'))


hiddenimports = [

    # scikit-learn
    'sklearn.pipeline',

    # spaCy models

    'en_core_web_sm',
]

# Exclusions
excludes = [
]

a = Analysis(
    ['ner_daemon_spacy.py'],
    pathex = [],
    binaries = [],
    datas = datas,
    hiddenimports = hiddenimports,
    hookspath = [],
    runtime_hooks = [],
    excludes = excludes,
    win_no_prefer_redirects = False,
    win_private_assemblies = False,
    cipher = block_cipher,
    noarchive = False
)

pyz = PYZ(
    a.pure,
    a.zipped_data,
    cipher = block_cipher
)

exe = EXE(
    pyz,
    a.scripts,
    debug = False,
    upx = True,
    name = 'nerd-spacy'
)
