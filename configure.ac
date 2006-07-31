AC_INIT(README)
AC_CANONICAL_SYSTEM

AM_INIT_AUTOMAKE(license-verifier-plugin, 0.1)

AM_MAINTAINER_MODE

MONO_REQUIRED_VERSION=1.1.13
PKG_CHECK_MODULES(MONO, mono >= $MONO_REQUIRED_VERSION)

dnl Mono programs
MONODIR=`$PKG_CONFIG --variable=exec_prefix mono`
MONOBIN="$MONODIR/bin"
MONO="$MONOBIN/mono"
MCS="$MONOBIN/mcs"
GMCS="$MONOBIN/gmcs"

if test ! -x "$MONO"; then
	AC_MSG_ERROR([No Mono runtime found])
fi

CS="C#"
if test ! -x "$MCS"; then
	AC_MSG_ERROR([No $CS compiler found])
fi

if test ! -x "$GMCS"; then
	AC_MSG_ERROR([No $CS 2.0 (gmcs) compiler found])
fi

MCS_FLAGS="-debug"

dnl Banshee deps
PKG_CHECK_MODULES(BANSHEE, banshee >= 0.10.2)
AC_SUBST(BANSHEE_LIBS)

if test "x`$PKG_CONFIG --variable=pluginapi banshee`" = "x2"; then
	banshee_0_10="no"
	MCS=$GMCS
	AM_CONDITIONAL(BANSHEE_0_10, false)
else
	banshee_0_10="yes"
	MCS_FLAGS="$MCS_FLAGS -define:BANSHEE_0_10"
	AM_CONDITIONAL(BANSHEE_0_10, true)
fi

AC_SUBST(MCS_FLAGS)
AC_SUBST(MCS)
AC_SUBST(MONO)

AC_ARG_ENABLE(user-plugin, AC_HELP_STRING([--enable-user-plugin], [Install plugin into user-level Banshee plugins directory instead of the Banshee system plugins directory]), , enable_user_plugin="no")

dnl Banshee Plugins Path
SYSTEM_PLUGINS=`$PKG_CONFIG --variable=systemplugindir banshee`
REL_PLUGINS_DIR=`basename $SYSTEM_PLUGINS`
REL_BANSHEE_DIR=`echo "$SYSTEM_PLUGINS" | sed -e "s|\/$REL_PLUGINS_DIR||"`
REL_BANSHEE_DIR=`basename $REL_BANSHEE_DIR`
REL_PLUGINS_DIR=$REL_BANSHEE_DIR/$REL_PLUGINS_DIR

expanded_libdir=`( case $prefix in NONE) prefix=$ac_default_prefix ;; *) ;; esac
		   case $exec_prefix in NONE) exec_prefix=$prefix ;; *) ;; esac
		   eval echo $libdir )`
		   
PLUGINDIR=$libdir/$REL_PLUGINS_DIR
expanded_pluginsdir=$expanded_libdir/$REL_PLUGINS_DIR

if test "x$enable_user_plugin" = "xyes"; then
	PLUGINDIR=`$PKG_CONFIG --variable=userplugindir banshee`
	PLUGINDIR=`echo "$PLUGINDIR" | sed -e "s|\~|$HOME|"`
    expanded_pluginsdir=$PLUGINDIR
fi

AC_SUBST(PLUGINDIR)

AC_OUTPUT([
	Makefile
	src/Makefile
])

echo "
Plugin will be installed in ${expanded_pluginsdir}
You should now run \`make'
";
